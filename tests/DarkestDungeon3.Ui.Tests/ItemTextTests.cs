using Assets.Code.Item;
using Assets.Code.Library;
using Assets.Code.Utils;
using DarkestDungeon3.Dd2;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class ItemTextTests
{
    [Fact]
    public void AColdMenuMissDoesNotPermanentlyEraseNativeEffectsAfterTheRunLoads()
    {
        const string id = "description_retry_test";
        SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance = null;
        Assert.Null(ItemText.Effects(id));
        var library = new Library<string, ItemDefinition>();
        library.Elements[id] = new ItemDefinition { Description = "<b>+15%</b> <sprite name=\"icon_move\"> RES" };
        SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance = library;
        Assert.Equal("+15% Move RES", ItemText.Effects(id));
        // A later native library must not inherit stale values from the previous run.
        var next = new Library<string, ItemDefinition>();
        next.Elements[id] = new ItemDefinition { Description = "+30% Move RES" };
        SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance = next;
        Assert.Equal("+30% Move RES", ItemText.Effects(id));
    }
}
