using MegaCrit.Sts2.Core.Combat;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Players;

// Monster requirements 6-9, 2026-11-15: real move bodies, percentage commands,
// timed Break callbacks and permanent-deck Hello World, with UI leaves recorded.
static partial class ActRosterChecks
{
    static bool KillHeroAfterAttack;
    static void StrengthenedEnemyCases()
    {
        foreach ((Type type, string move) in new[]
        {
            (typeof(CatHorn), "CREEPY_BALL_MOVE"), (typeof(CatHornKiller), "CREEPY_BALL_MOVE"),
            (typeof(DiamondEyeball), "LASER_BIND_MOVE"), (typeof(PillarGuardian), "PARALYSIS_BURST_MOVE"),
            (typeof(TargetWireFrame), "ILLUSION_SPIN_MOVE"), (typeof(TargetWireFrameKiller), "ILLUSION_SPIN_MOVE"),
            (typeof(PileGolemEdge), "CONFUSION_SPIKE_MOVE"), (typeof(OctopusTail), "EROSION_SLICE_MOVE"),
            (typeof(OctopusTailKiller), "EROSION_SLICE_MOVE")
        })
        {
            ResetTable(HeroKind.Hbr, 0, dp: 500);
            MonsterModel monster = TableActor(type, 0, out _);
            int startingHp = Hero.CurrentHp;
            try
            {
                KillHeroAfterAttack = true;
                MoveOf(monster, move).PerformMove([Hero]).GetAwaiter().GetResult();
                Check(!Hero.IsAlive && !Hero.Powers.Any(p => p is HbrInstancedEnemyStatusPower),
                    type.Name + " contact status excludes a target killed by its actual attack");
            }
            finally { KillHeroAfterAttack = false; Set(Hero, "_currentHp", startingHp); }
            ResetTable(HeroKind.Hbr, 0, dp: 500);
            monster = TableActor(type, 0, out _);
            MoveOf(monster, move).PerformMove([]).GetAwaiter().GetResult();
            Check(!Hero.Powers.Any(p => p is HbrInstancedEnemyStatusPower),
                type.Name + " contact status excludes a player outside the move target list");
        }
        MixedTeamControlCase();
        foreach (Type type in new[] { typeof(FlatHand), typeof(NararaShibaBossMonster), typeof(RedCrimson),
            typeof(RedCrimsonBossMonster), typeof(SkullFeather), typeof(TezukaSaki) })
        foreach (decimal turns in new[] { 2m, 99m })
        {
            ResetTable(HeroKind.Hbr, 0);
            Creature caster = Actor(type);
            foreach ((string apply, Type effect, decimal basis) in new[]
            {
                ("ApplyAttackDown", typeof(HbrAttackDownPower), 25m),
                ("ApplyDefenseDown", typeof(HbrDefenseDownPower), 50m)
            })
            {
                Await(Static("HBRGOSakiMod.Core.Helpers.HbrBossDebuffApplication", apply,
                    new ThrowingPlayerChoiceContext(), Hero, turns, caster, null));
                PowerModel power = Hero.Powers.Single(p => p.GetType() == effect);
                decimal casterE = (decimal)Static("HBRGOSakiMod.Core.Helpers.HbrEnemyProfileHelper", "GetEnemyParameter", caster)!;
                decimal heroE = (decimal)Static("HBRGOSakiMod.Core.Helpers.HbrEnemyProfileHelper", "GetEnemyParameter", Hero)!;
                Equal(EffectPercent(power), basis * (1m + (casterE - heroE) * 0.0002m),
                    type.Name + " actual Boss " + apply + " uses " + basis + "% basis");
                Equal(power.Amount, turns == 99m ? 1 : 3,
                    type.Name + " Boss " + apply + " preserves eternal/duration profile");
            }
        }
        foreach (Type type in new[] { typeof(RedCrimson), typeof(SkullFeather) })
        {
            ResetTable(HeroKind.Hbr, 0);
            Creature phantom = PhantomActor(type);
            Creature target = Enemy();
            foreach ((string apply, Type effect) in new[] { ("ApplyAttackDown", typeof(HbrAttackDownPower)),
                ("ApplyDefenseDown", typeof(HbrDefenseDownPower)) })
            {
                Await(Static("HBRGOSakiMod.Core.Helpers.HbrBossDebuffApplication", apply,
                    new ThrowingPlayerChoiceContext(), target, 2m, phantom, null));
                PowerModel power = target.Powers.Single(p => p.GetType() == effect);
                decimal expected = (decimal)Static("HBRGOSakiMod.Core.Helpers.HbrPercentDebuffHelper", "ResolveBasis",
                    power, 15m, phantom, target)!;
                Equal(EffectPercent(power), expected, type.Name + " player-owned phantom retains 15% Boss basis");
                Equal(power.Amount, 2, type.Name + " player-owned phantom retains enemy-side duration");
            }
        }
        BreakModeBoundaryCases();
        DedicatedBreakExclusionCases();
        HelloWorldBreakCases();
    }
    static void MixedTeamControlCase()
    {
        ResetTable(HeroKind.Hbr, 0);
        var original = Blank<Player>(); Set(original, "_runState", State<object>("RunState"));
        var originalState = Blank<PlayerCombatState>(); Set(originalState,"<TurnNumber>k__BackingField",1);
        Set(original,"<PlayerCombatState>k__BackingField",originalState);
        Set(original, "<Character>k__BackingField", Blank<Ironclad>()); Set(original, "_relics", new List<RelicModel>());
        Creature ally = (Creature)FixtureCall("Actor", null, original, null)!; Set(original, "<Creature>k__BackingField", ally);
        var savedPlayers = State<MegaCrit.Sts2.Core.Runs.RunState>("RunState").Players;
        try
        {
            Set(State<object>("RunState"), "_players", new List<Player> { HeroPlayer, original });
            Set(Combat, "_allies", new List<Creature> { Hero, ally });
            MonsterModel cat = TableActor(typeof(CatHorn), 0, out _);
            MoveOf(cat, "CREEPY_BALL_MOVE").PerformMove([ally]).GetAwaiter().GetResult();
            Check(ally.HasPower<HbrImprisonPower>() && !Hero.HasPower<HbrImprisonPower>(),
                "HBR enemy caster applies contact control to an original teammate even though that target has no HBR manual");
        }
        finally
        {
            Set(State<object>("RunState"), "_players", savedPlayers); Set(Combat, "_allies", new List<Creature> { Hero });
            ResetTable(HeroKind.Hbr, 0);
        }
    }
    static void BreakModeBoundaryCases()
    {
        foreach (HeroKind kind in HeroKinds)
        foreach ((Type room, int hbrTurns) in new[] { (typeof(SmallHoppersNormal), 2),
            (typeof(ShrimpCrabsElite), 3), (typeof(RedCrimsonBoss), 4) })
        {
            ResetTable(kind, 0); Set(Combat, "_encounter", Blank(room));
            Creature target = Actor(typeof(SmallHopper));
            HighDimensionalShellPower shell = Shell(target, 100m);
            int turns = hbrTurns + (kind == HeroKind.Hbr ? 0 : 1);
            Check((string)Call(shell, "GetDisplayedBreakAmount")! == turns.ToString(),
                kind + " " + room.Name + " mutable full/compact tooltip reads the actual mode-specific Break duration");
            Await(Call(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0));
            Equal(target.GetPowerAmount<BreakPower>(), turns, kind + " " + room.Name + " real Break application preserves its numeric mode boundary");
            BreakPower broken = target.GetPower<BreakPower>()!;
            for (int turn = 1; turn <= turns; turn++)
            {
                Await(broken.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [target], Combat));
                Check(turn < turns ? target.GetPowerAmount<BreakPower>() == turns - turn && !target.HasPower<HighDimensionalShellPower>() :
                    !target.HasPower<BreakPower>() && target.HasPower<HighDimensionalShellPower>(),
                    kind + " " + room.Name + " real Break tick " + turn + "/" + turns + " retains exact return timing");
            }
        }
    }
    static void DedicatedBreakExclusionCases()
    {
        foreach (Type type in new[] { typeof(SkullFeatherPower), typeof(IntangiblePower), typeof(SlipperyPower), typeof(DexterityPower) })
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        ResetTable(HeroKind.Hbr, 0);
        Creature feather = Actor(typeof(SkullFeather));
        Power<SkullFeatherPower>(feather, 1);
        HighDimensionalShellPower shell = Shell(feather, 100m);
        Await(Call(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0));
        Equal(feather.GetPowerAmount<BreakPower>(), 99, "SkullFeather's real shell-break observer retains its dedicated 99-turn Break");
        Await(feather.GetPower<BreakPower>()!.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [feather], Combat));
        Check(feather.GetPowerAmount<BreakPower>() == 98 && !feather.HasPower<HighDimensionalShellPower>(),
            "SkullFeather's own long Break does not accidentally use the new ordinary timer");

        ResetTable(HeroKind.Hbr, 0);
        Creature phantom = PhantomActor(typeof(SmallHopper)); Power<PhantomPower>(phantom, 1);
        shell = Shell(phantom, 100m);
        Await(Call(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0));
        Check(!phantom.HasPower<BreakPower>() && !phantom.HasPower<DestructionRatePower>() && !phantom.HasPower<HighDimensionalShellPower>(),
            "a real player-owned phantom still loses its empty shell without gaining Break or destruction");

        ResetTable(HeroKind.Hbr, 0);
        Creature tezuka = Actor(typeof(TezukaSaki));
        HighDimensionalShellPower derivative = SpecialShell(typeof(PartialDerivativeShieldPower), tezuka, 100m);
        Await(Call(derivative, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0));
        PermanentBreakPower permanent = tezuka.GetPower<PermanentBreakPower>()!;
        int before = permanent.Amount;
        for (int turn = 0; turn < 6; turn++)
            Await(permanent.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [tezuka], Combat));
        Check(permanent.Amount == before && tezuka.HasPower<GhostStatePower>() && !tezuka.HasPower<PartialDerivativeShieldPower>(),
            "Tezuka's actual permanent Break ignores all ordinary 2/3/4 countdowns");
    }
    static void HelloWorldBreakCases()
    {
        if (!ModelDb.Contains(typeof(HelloWorldHbrgoSaki))) ModelDb.Inject(typeof(HelloWorldHbrgoSaki));
        ResetTable(HeroKind.Hbr, 0);
        var original = HeroPlayer.Deck.Cards.ToArray();
        var hello = (HelloWorldHbrgoSaki)ModelDb.Card<HelloWorldHbrgoSaki>().ToMutable();
        Set(hello, "_owner", HeroPlayer);
        SetProp(hello, nameof(HelloWorldHbrgoSaki.UpgradeEffectOrder), new[] { 2 });
        SetProp(hello, nameof(CardModel.CurrentUpgradeLevel), 1);
        HeroPlayer.Deck.AddInternal(hello);
        try
        {
            foreach ((Type room, int turns) in new[] { (typeof(SmallHoppersNormal), 3),
                (typeof(ShrimpCrabsElite), 4), (typeof(RedCrimsonBoss), 5) })
            {
                ResetTable(HeroKind.Hbr, 0);
                Set(Combat, "_encounter", Blank(room));
                Creature target = Actor(typeof(SmallHopper));
                HighDimensionalShellPower shell = Shell(target, 100m);
                Await(Call(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0));
                Equal(target.GetPowerAmount<BreakPower>(), turns,
                    room.Name + " one owned Hello World adds one to the new Break base");
            }
        }
        finally
        {
            HeroPlayer.Deck.RemoveInternal(hello);
            ResetTable(HeroKind.Hbr, 0);
            Check(HeroPlayer.Deck.Cards.SequenceEqual(original), "Hello World Break check restores the original permanent deck");
        }
    }
}
