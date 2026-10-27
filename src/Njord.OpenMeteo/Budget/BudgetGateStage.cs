using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Stage;
using Njord.Messages.Budget;

namespace Njord.OpenMeteo.Budget;

internal sealed class BudgetGateStage<T> : GraphStage<FlowShape<T, T>>
{
    private sealed class Logic : TimerGraphStageLogic, IInHandler, IOutHandler
    {
        private const string RetryTimer = "retry";
        private const string RefreshTimer = "refresh";
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

        private readonly BudgetGateStage<T> _stage;
        private readonly IActorRef _tracker;
        private double _tokens;
        private double _tokensPerSecond;
        private int _maxBurst;
        private DateTimeOffset _lastRefill;
        private (T Element, int Weight)? _parked;
        private bool _willStop;

        public Logic(BudgetGateStage<T> stage) : base(stage.Shape)
        {
            _stage = stage;
            _tracker = stage._tracker;
            var rate = stage._provider.GetCurrentRate();
            _tokensPerSecond = rate.CostPerMinute / 60.0;
            _maxBurst = Math.Max(rate.MaxBurst, stage._maxCallWeight);
            _tokens = _maxBurst;
            _lastRefill = stage._timeProvider.GetUtcNow();
            SetHandler(stage.In, this);
            SetHandler(stage.Out, this);
        }

        public override void PreStart()
        {
            ScheduleRepeatedly(RefreshTimer, RefreshInterval, RefreshInterval);
        }

        public void OnPush()
        {
            var element = Grab(_stage.In);
            var weight = _stage._costFn(element);
            Refill();

            if (_tokens >= weight)
            {
                _tokens -= weight;
                _tracker.Tell(new RecordApiCall(weight));
                Push(_stage.Out, element);
            }
            else
            {
                _parked = (element, weight);
                ScheduleOnce(RetryTimer, EstimateDelay(weight));
            }
        }

        public void OnPull() => Pull(_stage.In);

        public void OnUpstreamFinish()
        {
            if (_parked is not null && IsTimerActive(RetryTimer))
                _willStop = true;
            else
                CompleteStage();
        }

        public void OnUpstreamFailure(Exception e) => FailStage(e);

        public void OnDownstreamFinish(Exception cause) => CancelStage(cause);

        protected override void OnTimer(object timerKey)
        {
            if (ReferenceEquals(timerKey, RefreshTimer))
            {
                var rate = _stage._provider.GetCurrentRate();
                _tokensPerSecond = rate.CostPerMinute / 60.0;
                _maxBurst = Math.Max(rate.MaxBurst, _stage._maxCallWeight);
                return;
            }

            Refill();
            var (elem, weight) = _parked!.Value;
            if (_tokens >= weight)
            {
                _tokens -= weight;
                _tracker.Tell(new RecordApiCall(weight));
                Push(_stage.Out, elem);
                _parked = null;
                if (_willStop) CompleteStage();
            }
            else
            {
                ScheduleOnce(RetryTimer, EstimateDelay(weight));
            }
        }

        private void Refill()
        {
            var now = _stage._timeProvider.GetUtcNow();
            var elapsed = (now - _lastRefill).TotalSeconds;
            _lastRefill = now;
            _tokens = Math.Min(_tokens + elapsed * _tokensPerSecond, _maxBurst);
        }

        private TimeSpan EstimateDelay(int weight)
        {
            var deficit = weight - _tokens;
            if (deficit <= 0) return TimeSpan.FromMilliseconds(10);
            var seconds = deficit / _tokensPerSecond;
            return seconds < 0.01
                ? TimeSpan.FromMilliseconds(10)
                : TimeSpan.FromSeconds(seconds);
        }
    }

    private readonly IBudgetProvider _provider;
    private readonly IActorRef _tracker;
    private readonly TimeProvider _timeProvider;
    private readonly Func<T, int> _costFn;
    private readonly int _maxCallWeight;

    public BudgetGateStage(
        IBudgetProvider provider, IActorRef tracker, TimeProvider timeProvider,
        Func<T, int> costFn, int maxCallWeight)
    {
        _provider = provider;
        _tracker = tracker;
        _timeProvider = timeProvider;
        _costFn = costFn;
        _maxCallWeight = Math.Max(maxCallWeight, 1);
        Shape = new FlowShape<T, T>(In, Out);
    }

    public Inlet<T> In { get; } = new("BudgetGate.in");
    public Outlet<T> Out { get; } = new("BudgetGate.out");
    public override FlowShape<T, T> Shape { get; }

    protected override GraphStageLogic CreateLogic(Attributes inheritedAttributes) => new Logic(this);
}
