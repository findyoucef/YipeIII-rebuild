using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private int dartsHighScore;
    private void ShowCasino() => Choices("Yipe! Casino", $"Welcome! Gold {Hero.Gold}", [
        new("Slots", ShowSlots), new("Dot Racing", ShowDotRacing), new("Darts", ShowDarts), new("Leave", AdvanceCommand)], AdvanceCommand);
    private void ShowSlots()
    {
        var state = new SlotMachineState { Gold = Hero.Gold };
        void Leave() { Hero.Gold = state.Gold; ShowCasino(); }
        void Menu() => Choices("Slots", $"Gold {state.Gold}  Credits {state.Credits}\nA spin costs 10 credits.", [
            new("Insert 10 gold", () => { if (Casino.InsertCredits(state)) Menu(); else Notice("Slots", "You do not have enough gold.", Menu); }),
            new("Spin", () => { var result = Casino.Spin(state, session.Random); if (result.Payout > 0) audio.Play(145); Notice("Slots", result.Spun ? string.Join(" / ", result.Symbols) + $"\nPayout: {result.Payout} credits" : "Insert credits first.", Menu); }),
            new("Collect credits", () => { Casino.CollectCredits(state); Menu(); }), new("Leave", Leave)], Leave);
        Menu();
    }
    private void ChooseWager(CasinoWager wager, Action back) => Choices("Place a bet", $"Gold {wager.Gold}  Bet {wager.Bet}\nMaximum bet: {Casino.MaximumBet}",
        new[] { 5, 10, 25, 50, 100 }.Select(amount => new OverlayOption($"Add {amount} gold", () =>
        { var result = Casino.PlaceWager(wager, amount); if (result == WagerStatus.Placed) back(); else Notice("Bet", result == WagerStatus.MaximumReached ? "The maximum bet is 250 gold." : "You do not have enough gold.", back); })), back);
    private void ShowDotRacing()
    {
        var wager = new CasinoWager { Gold = Hero.Gold };
        var selectedDot = 0;
        void Leave() { Casino.ClearWager(wager); Hero.Gold = wager.Gold; ShowCasino(); }
        void Menu() => Choices("Dot Racing", $"Gold {wager.Gold}  Bet {wager.Bet}\nSelected racer: {selectedDot}", [
            new("Add bet", () => ChooseWager(wager, Menu)), new("Clear bet", () => { Casino.ClearWager(wager); Menu(); }),
            new("Change racer", () => { selectedDot = selectedDot % 3 + 1; Menu(); }), new("Race", () =>
            { var result = Casino.Race(wager, selectedDot, session.Random); if (result.Raced) audio.Play(35); Notice("Dot Racing", result.Raced ? $"Racer {result.Winner} wins!\nPayout: {result.Payout} gold" : wager.Bet == 0 ? "Place a bet first." : "Choose a racer first.", Menu); }), new("Leave", Leave)], Leave);
        Menu();
    }
    private void ShowDarts()
    {
        var wager = new CasinoWager { Gold = Hero.Gold };
        var scores = new int[3];
        var throwIndex = 0;
        var aim = Casino.InitialDartAim;
        void Leave() { Hero.Gold = wager.Gold; ShowCasino(); }
        void Menu() => Choices("Darts", $"Gold {wager.Gold}  Bet {wager.Bet}  Aim {aim}\nScores: {string.Join(", ", scores)}\nToday's high score: {dartsHighScore}", [
            new("Add bet", () => { if (throwIndex == 0) ChooseWager(wager, Menu); else Notice("Darts", "Finish the current round first.", Menu); }),
            new("Clear bet", () => { if (throwIndex == 0) Casino.ClearWager(wager); Menu(); }),
            new("Aim up", () => { aim = Casino.AdjustDartAim(aim, true); Menu(); }),
            new("Aim down", () => { aim = Casino.AdjustDartAim(aim, false); Menu(); }), new("Throw", Throw), new("Leave", Leave)], Leave);
        void Throw()
        {
            if (wager.Bet == 0) { Notice("Darts", "Place a bet first.", Menu); return; }
            audio.Play(148);
            var result = Casino.ThrowDart(aim, throwIndex, session.Random);
            audio.Play(147);
            scores[throwIndex++] = result.Score; aim = Casino.InitialDartAim;
            if (throwIndex < 3) { Notice("Darts", $"Throw {throwIndex} scored {result.Score}.", Menu); return; }
            var total = scores.Sum();
            var payout = Casino.DartPayout(scores, wager.Bet);
            wager.Gold += payout; wager.Bet = 0; dartsHighScore = Math.Max(dartsHighScore, total);
            Notice("Darts", $"Throw 3 scored {result.Score}.\nRound score: {total}\nPayout: {payout} gold", () => { Array.Clear(scores); throwIndex = 0; Menu(); });
        }
        Menu();
    }
}
