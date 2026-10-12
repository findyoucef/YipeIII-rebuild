using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Yipe.Desktop;

internal static class IceMotionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var input = new GameInput();
        input.Update(new(Keys.Right), default, .016);
        check(input.Update(new(Keys.Right), default, .016).Direction == Point.Zero && input.HeldDirection == new Point(1, 0),
            "Ice steering retains keyboard intent between repeat pulses");
        input.Update(default, default, .016);
        check(input.HeldDirection == Point.Zero, "Ice steering detects release immediately");
        input.Update(new(Keys.Right), default, .016, active: false);
        input.Update(new(Keys.Right), default, .016);
        check(input.HeldDirection == Point.Zero, "Ice steering suppresses held keys on focus return");
        input.Update(new(Keys.Right), default, .3);
        check(input.HeldDirection == new Point(1, 0), "Ice steering resumes held movement after the focus delay");
        var pad = new GameInput();
        GamePadState Pad(Vector2 stick, params Buttons[] buttons) => new(stick, Vector2.Zero, 0, 0, buttons);
        pad.Update(default, Pad(Vector2.Zero), .016);
        pad.Update(default, Pad(Vector2.Zero, Buttons.DPadLeft), .016);
        pad.Update(default, Pad(Vector2.Zero, Buttons.DPadLeft), .016);
        check(pad.HeldDirection == new Point(-1, 0), "Ice steering retains D-pad intent between pulses");
        pad.Update(default, Pad(new(.8f, .8f)), .016);
        check(pad.HeldDirection == new Point(1, -1), "Ice steering uses the controller's eight-way stick sectors");
        pad.Update(default, Pad(new(.2f, .1f)), .016);
        check(pad.HeldDirection == Point.Zero, "Controller dead zone releases the ice glide");
        pad.Update(default, default, .016);
        pad.Update(default, Pad(new(.8f, 0)), .016);
        check(pad.HeldDirection == Point.Zero, "Controller reconnect cannot replay ice movement");
        var motion = new PlayerMotion(new(5, 5));
        motion.MoveTo(new(6, 5), PlayerStride.IcePush); motion.Update(.17);
        check(motion.IsMoving && Math.Abs(motion.VisualPosition.X - 5.375f) < .0001,
            "Ice push-off accelerates and takes longer than a normal step");
        motion.Update(.17); motion.MoveTo(new(7, 5), PlayerStride.IceGlide); motion.Update(.11);
        check(motion.IsSliding && motion.AnimationFrame == 0 && motion.VisualPosition.X == 6.5f,
            "Held ice travel reaches a faster glide with planted feet");
        motion.Update(.11); motion.MoveTo(new(8, 5), PlayerStride.IceBrake); motion.Update(.21);
        check(motion.IsSliding && motion.VisualPosition.X == 7.75f,
            "Released ice travel decelerates smoothly toward its final tile");
        motion.Update(10);
        check(!motion.IsMoving && motion.VisualPosition == new Vector2(8, 5), "A delayed ice frame cannot overshoot");
        motion.MoveTo(new(9, 5), PlayerStride.IceGlide); motion.SnapTo(new(4, 4)); motion.MoveTo(new(5, 4)); motion.Update(.125);
        check(!motion.IsSliding && motion.VisualPosition == new Vector2(4.5f, 4), "Restore clears ice timing and ordinary walking stays linear");
        var slow = new PlayerMotion(new(5, 5)); var fast = new PlayerMotion(new(5, 5));
        slow.MoveTo(new(6, 6), PlayerStride.IceBrake); fast.MoveTo(new(6, 6), PlayerStride.IceBrake);
        slow.Update(.2); for (var i = 0; i < 20; i++) fast.Update(.01);
        check(Vector2.Distance(slow.VisualPosition, fast.VisualPosition) < .0001f,
            "Diagonal ice motion depends on elapsed time, not frame rate");
    }
}
