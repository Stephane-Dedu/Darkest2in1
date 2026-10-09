using System.Reflection;
using DarkestDungeon3.Core.Campaign;
using DarkestDungeon3.Core.Dd1;
using DarkestDungeon3.Core.Expedition;
using DarkestDungeon3.Ui;
using UnityEngine;
using Xunit;

namespace DarkestDungeon3.Ui.Tests;

public class ProvisionCancelTests : IDisposable
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(EmbarkUi ui, string name) => (T)typeof(EmbarkUi).GetField(name, Private).GetValue(ui);
    private static void Set(EmbarkUi ui, string name, object value) => typeof(EmbarkUi).GetField(name, Private).SetValue(ui, value);
    private static bool DrawBack(EmbarkUi ui) => (bool)typeof(EmbarkUi).GetMethod("DrawProvisioningBack", Private).Invoke(ui, null);
    private static void Input(EventType type, float x = 170, float y = 1030, int button = 0)
        => Event.current = new Event { type = type, rawType = type, mousePosition = new Vector2(x, y), button = button };

    public void Dispose() { GUI.enabled = true; Input(EventType.Repaint); }

    private static EmbarkUi Prepared()
    {
        var ui = new EmbarkUi();
        Set(ui, "_provisioning", true);
        Set(ui, "_confirmLow", true);
        Set(ui, "_error", "Previous embark failed");
        var cart = Get<Inventory>(ui, "_cart");
        cart.Add(Supply.Food, 12);
        cart.Add(Supply.Key, 2);
        cart.Layout.AddRange(new[] { Supply.Key, null, Supply.Food });
        return ui;
    }

    [Fact]
    public void BackClearsStagedPurchasesLayoutAndPromptsButPreservesSelection()
    {
        var ui = Prepared();
        var quest = new QuestOffer { Id = "selected" };
        Set(ui, "_quest", quest);
        var party = Get<List<string>>(ui, "_party");
        party.AddRange(new[] { "hero-a", "hero-b" });
        Input(EventType.MouseUp);
        Assert.True(DrawBack(ui));
        var cart = Get<Inventory>(ui, "_cart");
        Assert.Empty(cart.Items);
        Assert.Empty(cart.Layout);
        Assert.False(Get<bool>(ui, "_provisioning"));
        Assert.False(Get<bool>(ui, "_confirmLow"));
        Assert.Null(Get<string>(ui, "_error"));
        Assert.Same(quest, Get<QuestOffer>(ui, "_quest"));
        Assert.Equal(new[] { "hero-a", "hero-b" }, party);
        Assert.False(DrawBack(ui)); // the same input has already been consumed
    }

    [Fact]
    public void ReopenedCartOnlyCarriesNewPurchasesIntoTheNextExpedition()
    {
        var ui = Prepared();
        Input(EventType.MouseUp);
        Assert.True(DrawBack(ui));
        var cart = Get<Inventory>(ui, "_cart");
        Set(ui, "_provisioning", true);
        cart.Add(Supply.Torch, 3);
        var dd1 = Dd1Campaign.Load(Dd1Install.Find());
        var quest = new QuestOffer { Id = "next", Dungeon = "crypts", Type = "explore", Length = 1, Difficulty = 1, MapSeed = 72 };
        Set(ui, "_quest", quest);
        var hero = new HeroRecord { Id = "new-party", ClassId = "highwayman" };
        var bought = new Inventory { Layout = new List<string>(cart.Layout) };
        foreach (var kv in cart.Items) bought.Add(kv.Key, kv.Value);
        var exp = Embark.Create(dd1, quest, new[] { hero }, bought);
        Assert.Equal(3, exp.Pack.Count(Supply.Torch));
        Assert.Equal(0, exp.Pack.Count(Supply.Food));
        Assert.Equal(0, exp.Pack.Count(Supply.Key));
        Assert.Empty(exp.Pack.Layout);
        Assert.Equal(new[] { hero.Id }, exp.Party);
    }

    [Theory]
    [InlineData(EventType.Repaint, true, 170, 1030, 0)]
    [InlineData(EventType.MouseUp, false, 170, 1030, 0)]
    [InlineData(EventType.MouseUp, true, 600, 500, 0)]
    [InlineData(EventType.MouseUp, true, 170, 1030, 1)]
    public void OtherInputPreservesThePendingPurchase(EventType type, bool enabled, float x, float y, int button)
    {
        var ui = Prepared();
        GUI.enabled = enabled;
        Input(type, x, y, button);
        Assert.False(DrawBack(ui));
        Assert.Equal(12, Get<Inventory>(ui, "_cart").Count(Supply.Food));
        Assert.Equal(3, Get<Inventory>(ui, "_cart").Layout.Count);
        Assert.True(Get<bool>(ui, "_provisioning"));
        Assert.True(Get<bool>(ui, "_confirmLow"));
        Assert.Equal("Previous embark failed", Get<string>(ui, "_error"));
    }
}
