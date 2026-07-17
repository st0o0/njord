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

    private sealed class Logic : TimerGraphStageLogic
    {
        private const string EmitTimerKey = "emit";

        private readonly BudgetThrottleStage<T> _stage;
        private T? _pending;
        private bool _hasPending;
        private bool _upstreamFinished;

        public Logic(BudgetThrottleStage<T> stage) : base(stage.Shape)
        {
            _stage = stage;

            SetHandler(stage.In, onPush: OnPush, onUpstreamFinish: () =>
            {
                if (!_hasPending)
                    CompleteStage();
                else
                    _upstreamFinished = true;
            });

            SetHandler(stage.Out, onPull: () =>
            {
                if (!_hasPending && !_upstreamFinished)
                    Pull(stage.In);
            });
        }

        private void OnPush()
        {
            var element = Grab(_stage.In);

            if (_stage._gate.TryAcquire(element))
            {
                Push(_stage.Out, element);
            }
            else
            {
                _pending = element;
                _hasPending = true;
                ScheduleOnce(EmitTimerKey, _stage._gate.EstimateDelay(element));
            }
        }

        protected override void OnTimer(object timerKey)
        {
            if (!_hasPending)
                return;

            if (_stage._gate.TryAcquire(_pending!))
            {
                Push(_stage.Out, _pending!);
                _pending = default;
                _hasPending = false;

                if (_upstreamFinished)
                {
                    CompleteStage();
                    return;
                }

                if (IsAvailable(_stage.Out))
                    Pull(_stage.In);
            }
            else
            {
                ScheduleOnce(EmitTimerKey, _stage._gate.EstimateDelay(_pending!));
            }
        }
    }
}
