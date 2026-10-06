namespace Yipe.Core;

public sealed class CasinoWager { public int Gold { get; set; } public int Bet { get; set; } }
public sealed class SlotMachineState { public int Gold { get; set; } public int Credits { get; set; } }
public enum WagerStatus { Placed, InsufficientGold, MaximumReached, InvalidAmount }
public readonly record struct SlotSpinResult(bool Spun, int[] Symbols, int Payout);
public readonly record struct DotRaceResult(bool Raced, int Winner, int Payout, int[] FinalPositions);
public readonly record struct DartThrowResult(int FinalY, int Score);

/// <summary>Original casino rules. Animation does not control or consume their RNG draws.</summary>
public static class Casino
{
    public const int MaximumBet = 250;
    public const int InitialDartAim = 36;

    public static WagerStatus PlaceWager(CasinoWager wager, int amount)
    {
        if (amount <= 0) return WagerStatus.InvalidAmount;
        if ((long)wager.Bet + amount > MaximumBet) return WagerStatus.MaximumReached;
        if (wager.Gold < amount) return WagerStatus.InsufficientGold;
        wager.Gold -= amount; wager.Bet += amount;
        return WagerStatus.Placed;
    }

    public static void ClearWager(CasinoWager wager) { wager.Gold += wager.Bet; wager.Bet = 0; }
    public static bool InsertCredits(SlotMachineState state)
    {
        if (state.Gold < 10) return false;
        state.Gold -= 10; state.Credits += 10;
        return true;
    }
    public static void CollectCredits(SlotMachineState state) { state.Gold += state.Credits; state.Credits = 0; }

    public static SlotSpinResult Spin(SlotMachineState state, MsvcRandom random)
    {
        var symbols = new int[3];
        if (state.Credits < 10) return new(false, symbols, 0);
        state.Credits -= 10;
        var roll = random.Next() % 100;
        var forced = roll < 64 ? 0 : roll < 74 ? 15 : roll < 79 ? 35 : roll < 83 ? 60 : roll < 85 ? 80 : roll == 85 ? 150 : 5;
        for (var frame = 0; frame < 25; frame++)
        {
            if (frame < 17) symbols[0] = frame == 16 && forced != 0 ? forced : Symbol();
            if (frame < 21) symbols[1] = frame == 20 && forced != 0 ? forced : Symbol();
            symbols[2] = frame == 24 && forced != 0 ? forced : Symbol();
        }
        var payout = symbols[0] == symbols[1] && symbols[0] == symbols[2] ? symbols[0] : 0;
        state.Credits += payout;
        return new(true, symbols, payout);

        int Symbol()
        {
            var value = random.Next() % 21;
            return value < 6 ? 5 : value < 11 ? 15 : value < 15 ? 35 : value < 18 ? 60 : value < 20 ? 80 : 150;
        }
    }

    public static DotRaceResult Race(CasinoWager wager, int selectedDot, MsvcRandom random)
    {
        if (wager.Bet <= 0 || selectedDot is < 1 or > 3) return new(false, 0, 0, new int[3]);
        var bet = wager.Bet; wager.Bet = 0;
        int[] positions = [70, 72, 72];
        while (positions.Max() < 482)
            for (var i = 0; i < 3; i++) positions[i] += random.Next() % 15;
        var winner = positions[0] > positions[1] && positions[0] > positions[2] ? 1
            : positions[1] > positions[0] && positions[1] > positions[2] ? 2 : 3;
        var payout = winner == selectedDot ? bet * 250 / 100 : 0;
        wager.Gold += payout;
        return new(true, winner, payout, positions);
    }

    public static int AdjustDartAim(int currentY, bool upward) => upward ? Math.Max(16, currentY - 5)
        : currentY > 75 ? currentY : currentY + 5;

    public static DartThrowResult ThrowDart(int aimY, int throwIndex, MsvcRandom random)
    {
        var y = aimY;
        for (var right = 58; right < 450;)
        {
            right += random.Next() % 15 + 5;
            var drift = random.Next() % 100;
            if (drift > 55) y += random.Next() % 3 + 1;
            else if (drift < 45) y -= random.Next() % 3 + 1;
            y = Math.Clamp(y, 16, 75);
        }
        var triple = random.Next() % 100 > 90;
        var threshold = (y == 51 ? 74 : 84) - (throwIndex == 1 ? 4 : 0);
        var doubled = random.Next() % 100 > threshold;
        if (y is < 24 or > 67) return new(y, 0);
        if (y is >= 42 and <= 50) return new(y, y is >= 45 and <= 47 ? 50 : 25);
        var score = y is >= 35 and <= 56 ? random.Next() % 11 + 10 : random.Next() % 9 + 1;
        return new(y, score * (doubled ? 2 : triple ? 3 : 1));
    }

    public static int DartPayout(ReadOnlySpan<int> scores, int bet)
    {
        if (scores.Length != 3) throw new ArgumentException("A dart round requires three scores.", nameof(scores));
        if (bet <= 0) return 0;
        var total = scores[0] + scores[1] + scores[2];
        return bet * (total == 150 ? 10 : total >= 120 ? 8 : total >= 105 ? 6 : total >= 88 ? 4 : total >= 73 ? 2 : total >= 55 ? 1 : 0);
    }
}
