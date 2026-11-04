using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Yipe.Desktop;

internal static class TouchInputChecks
{
    public static void Run(Action<bool, string> check)
    {
        var input = new TouchInput { Pad = new(20, 100, 90, 90) };
        input.Regions.Add(new(new(200, 120, 60, 40), new(default, true, false, false), 3));
        TouchLocation Finger(int id, TouchLocationState state, float x, float y) => new(id, state, new(x, y));
        InputFrame Send(params TouchLocation[] fingers) => input.Update(new(fingers));
        Send(Finger(1, TouchLocationState.Pressed, 100, 110));
        check(input.HeldDirection == new Point(1, -1), "Touch D-pad supports diagonal intent");
        var frame = Send(Finger(1, TouchLocationState.Moved, 100, 145), Finger(2, TouchLocationState.Pressed, 230, 140));
        check(input.HeldDirection == new Point(1, 0) && !frame.Confirm, "Movement and action fingers coexist without press-to-activate");
        frame = Send(Finger(1, TouchLocationState.Moved, 100, 145), Finger(2, TouchLocationState.Released, 230, 140));
        check(frame.Confirm && input.SelectedRow == 3 && input.HeldDirection == new Point(1, 0), "A released action activates once while movement remains held");
        check(!Send(Finger(1, TouchLocationState.Moved, 100, 145)).Confirm, "Holding movement never repeats an action");
        Send(Finger(1, TouchLocationState.Moved, 65, 145));
        check(input.HeldDirection == Point.Zero, "D-pad center has a dead zone");
        Send();
        check(input.HeldDirection == Point.Zero, "A canceled or missing movement contact cannot leave movement stuck");
        Send(Finger(2, TouchLocationState.Pressed, 230, 140));
        check(!Send(Finger(2, TouchLocationState.Released, 290, 140)).Confirm, "Dragging off a button cancels the tap");
        input.Reset();
        Send(Finger(1, TouchLocationState.Moved, 100, 145));
        check(input.HeldDirection == Point.Zero && !Send(Finger(2, TouchLocationState.Released, 230, 140)).Confirm,
            "Screen/context changes suppress old contacts until every finger lifts");
        Send(); Send(Finger(1, TouchLocationState.Pressed, 100, 145));
        check(input.HeldDirection == new Point(1, 0), "Fresh touch resumes after context suppression");
        input.Update(new([Finger(1, TouchLocationState.Moved, 100, 145)]), active: false);
        check(input.HeldDirection == Point.Zero, "Focus loss immediately clears touch movement");
        Send(); input.CanSwipe = true;
        Send(Finger(3, TouchLocationState.Pressed, 230, 140));
        frame = Send(Finger(3, TouchLocationState.Moved, 230, 110));
        check(frame.Direction == new Point(0, 1) && !Send(Finger(3, TouchLocationState.Released, 230, 110)).Confirm,
            "Scrolling advances choices without accidentally selecting the pressed row");
        input.Regions.Add(new(new(200, 60, 100, 20), default, 4, Slider: true));
        Send(Finger(4, TouchLocationState.Pressed, 250, 70));
        check(input.SliderRow == 4 && input.SliderValue == .5f && !input.SliderFinished, "Slider presses set an absolute value");
        frame = Send(Finger(4, TouchLocationState.Released, 350, 70));
        check(input.SliderValue == 1 && input.SliderFinished && !frame.Confirm && frame.Direction == Point.Zero,
            "Slider drags clamp and finish without becoming taps or scrolls");
        var movement = new GameInput();
        check(movement.Update(default, default, .016, touchDirection: new(1, -1)).Direction == new Point(1, -1)
            && movement.Update(default, default, .1, touchDirection: new(1, -1)).Direction == Point.Zero
            && movement.Update(default, default, .2, touchDirection: new(1, -1)).Direction == new Point(1, -1),
            "Touch movement uses the existing immediate step and held repeat cadence");
        check(movement.Update(new KeyboardState(Keys.Left), default, .016, touchDirection: new(1, 0)).Direction == new Point(-1, 0),
            "Physical direction retains priority over touch intent");
    }
}
