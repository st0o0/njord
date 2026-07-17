using Akka.Streams;
using Akka.Streams.Stage;

namespace Njord.Pipeline;

public sealed class BudgetThrottleStage<T> : GraphStage<FlowShape<T, T>>
{
    private readonly IBudgetGate<T> _gate;

    public BudgetThrottleStage(IBudgetGate<T> gate)
    {
        _gate = gate;
        Shape = new FlowShape<T, T>(In, Out);
    }

    public Inlet<T> In { get; } = new("BudgetThrottle.in");
    public Outlet<T> Out { get; } = new("BudgetThrottle.out");
    public override FlowShape<T, T> Shape { get; }

    protected override GraphStageLogic CreateLogic(Attributes inheritedAttributes)
        => new Logic(this);

    private sealed class Logic : GraphStageLogic
    {
        private readonly BudgetThrottleStage<T> _stage;
        private readonly Action<T> _onAcquired;
        private bool _upstreamFinished;
        private bool _downstreamPulled;
        private bool _acquiring;

        public Logic(BudgetThrottleStage<T> stage) : base(stage.Shape)
        {
            _stage = stage;
            _onAcquired = GetAsyncCallback<T>(OnAcquired);

            SetHandler(stage.In, onPush: OnPush, onUpstreamFinish: () =>
            {
                if (!_acquiring)
                    CompleteStage();
                else
                    _upstreamFinished = true;
            });
            SetHandler(stage.Out, onPull: () =>
            {
                _downstreamPulled = true;
                if (!_upstreamFinished)
                    Pull(stage.In);
            });
        }

        private void OnPush()
        {
            var element = Grab(_stage.In);
            _downstreamPulled = false;
            _acquiring = true;

            _stage._gate.AcquireAsync(element)
                .ContinueWith(_ => _onAcquired(element));
        }

        private void OnAcquired(T element)
        {
            _acquiring = false;
            Push(_stage.Out, element);

            if (_upstreamFinished)
            {
                CompleteStage();
                return;
            }

            if (_downstreamPulled)
                Pull(_stage.In);
        }
    }
}
