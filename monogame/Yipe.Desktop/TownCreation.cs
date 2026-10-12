using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class TownGame
{
    private Action<char>? textReceiver;
    private MsvcRandom creationRandom = new(1);
    private void ShowWelcome() => Choices("Yipe! III", "The King's stamps and daughters are missing. Your adventure begins in Maynard.", [
        new("New adventure", BeginCharacterCreation), new("Load saved adventure", () => { overlay = null; paused = true; menuMessage = "Loading..."; loading = saves.LoadAsync(); }),
        new("Quit", Exit)]);
    private void ShowNewGameConfirmation()
    {
        var previous = overlay;
        var previousTextReceiver = textReceiver;
        textReceiver = null;
        void KeepPlaying() { overlay = previous; textReceiver = previousTextReceiver; }
        Choices("New adventure?", "Unsaved progress in this adventure will be lost.", [
            new("Keep playing", KeepPlaying), new("Start a new adventure", BeginCharacterCreation)], KeepPlaying);
    }
    private void BeginCharacterCreation() { creationRandom = new(1); ShowCharacterType(); }
    private void ShowCharacterType()
    {
        Choices("Create Character", "Choose starting attributes. Random can be rerolled before starting.",
            new[] { "Combat", "Quickness", "Magic", "Random" }.Select((name, index) => new OverlayOption(name, () =>
            {
                var candidate = new Player(index == 3 ? CharacterType.Warrior : (CharacterType)(index + 1));
                if (index == 3) candidate.RerollRandomCharacter(creationRandom);
                ShowCharacterStats(candidate, index == 3, creationRandom);
            })), ShowWelcome);
    }
    private void ShowCharacterStats(Player candidate, bool random, MsvcRandom creationRandom)
    {
        var options = new List<OverlayOption> { new("Use these attributes", () => RequestText("Character Name", "Enter a name, or leave blank for Yipie.", "", name =>
        {
            candidate.Name = string.IsNullOrWhiteSpace(name) ? "Yipie" : name;
            Choices("Difficulty", "Choose the challenge for this adventure.", new[] { "Easy", "Medium", "Hard" }.Select((label, index) => new OverlayOption(label, () =>
            {
                candidate.Difficulty = index + 1;
                Choices("Adventure Style", "Silly Mode uses the original comic equipment names.", [new("Classic", () => Portraits(false)), new("Silly Mode", () => Portraits(true))], ShowCharacterType);
                void Portraits(bool silly)
                {
                    string[] names = silly ? ["Rooster", "Fairy", "Smiley", "Little Imp"]
                        : ["Knight", "Golden Knight", "Ranger", "Rose Mage", "Sorceress", "Wizard", "Violet Mage", "Bear"];
                    var first = silly ? 9 : 1;
                    overlay = new Overlay("Choose Appearance", "Your appearance does not change your attributes.",
                        names.Select((name, index) => new OverlayOption(name, () => Begin(candidate, first + index))), ShowCharacterType)
                        { FirstPortrait = first };
                }
            })), ShowCharacterType);
        }, ShowCharacterType)) };
        if (random) options.Add(new("Reroll", () => { candidate.RerollRandomCharacter(creationRandom); ShowCharacterStats(candidate, true, creationRandom); }));
        options.Add(new("Change specialty", ShowCharacterType));
        Choices("Attributes", $"Strength {candidate.Strength}  Dexterity {candidate.Dexterity}\nIntelligence {candidate.Intelligence}\nHP {candidate.Health}  SP {candidate.SpellPoints}  AP {candidate.AbilityPoints}", options, ShowCharacterType);
        void Begin(Player created, int portrait)
        {
            created.SillyMode = portrait >= 9; created.Inventory[28] = (ushort)portrait;
            command?.Dispose(); command = null; pendingEvent = null;
            journal = new(); mapOpen = false;
            session = new GameSession(catalog, created, randomState: creationRandom.State);
            dayNightSeconds = DayNightCycle.OpeningTime;
            adventure = new Adventure(session);
            SynchronizeWorld(false); tickRemaining = .5;
            Notice("The King", $"Hello {created.Name}.\n\nThe city of Yipe! needs you once again!\n\nMy collection of six rare stamps has been stolen!  I know that you are a brave and loyal adventurer.  Therefore I am giving this very important mission to you.\n\nPlease search the world and return my stamps to me.  Oh... I almost forgot to mention something else.  All of my daughters are missing.  I have no idea where they have gone.\n\nI will highly reward you for returning both my stamp collection and all of my daughters.  (I don't really remember exactly how many princesses I have.)\n\nTake this simple weapon and armor and begin your exploring.  Be sure to stop by my castle from time to time to check in with me.");
        }
    }

    private void RequestText(string title, string prompt, string initial, Action<string> done, Action cancel)
    {
        var value = initial;
        void Finish() { textReceiver = null; done(value); }
        void Cancel() { textReceiver = null; cancel(); }
        void Refresh()
        {
            Choices(title, prompt + "\n\n" + value + "_", [new("Done", Finish), new("Delete last character", Delete), new("Controller keyboard", Letters)], Cancel);
            textReceiver = character =>
            {
                if (character == '\b') { Delete(); return; }
                if (character is < ' ' or > '~' || value.Length >= 128) return;
                value += character; Refresh();
            };
        }
        void Delete() { if (value.Length > 0) value = value[..^1]; Refresh(); }
        void Letters()
        {
            textReceiver = null;
            var options = "abcdefghijklmnopqrstuvwxyz0123456789 -'.".Select(c => new OverlayOption(c == ' ' ? "Space" : c.ToString(), () =>
            { if (value.Length < 128) value += c; Letters(); })).ToList();
            options.Insert(0, new("Done typing", Refresh));
            options.Insert(1, new("Delete", () => { if (value.Length > 0) value = value[..^1]; Letters(); }));
            Choices(title, value + "_", options, Refresh);
        }
        Refresh();
    }
}
