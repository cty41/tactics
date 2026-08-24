using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Content;

namespace Tactics.Core.Tests;

[TestFixture]
public class DemonboundPossessedFormTests
{
    [Test]
    public void DefaultState_HasNoFormIdentity()
    {
        var state = new DemonboundBattleState();

        Assert.Multiple(() =>
        {
            Assert.That(state.IsPossessed, Is.False);
            Assert.That(state.PossessedFormId, Is.Null);
            Assert.That(state.PossessedBoostConfigurationId, Is.Null);
            Assert.That(state.PossessedBoostApplied, Is.False);
        });
    }

    [Test]
    public void PossessedConstructor_BindsDefaultFormAndBoostConfiguration()
    {
        var state = new DemonboundBattleState(10, 3, isPossessed: true);

        Assert.Multiple(() =>
        {
            Assert.That(state.IsPossessed, Is.True);
            Assert.That(state.PossessedFormId, Is.EqualTo(DemonboundBattleState.DefaultPossessedFormId));
            Assert.That(state.PossessedBoostConfigurationId,
                Is.EqualTo(DemonboundBattleState.DefaultPossessedBoostConfigurationId));
            Assert.That(state.PossessedBoostApplied, Is.False);
        });
    }

    [Test]
    public void ExplicitFormId_IsPreservedAsTheSingleSourceOfTruth()
    {
        var form = new ContentId("demonbound.possessed-form.v2");
        var boost = new ContentId("demonbound.possessed-boost.v2");
        var state = new DemonboundBattleState(10, 3, possessedFormId: form, possessedBoostConfigurationId: boost);

        Assert.Multiple(() =>
        {
            Assert.That(state.IsPossessed, Is.True);
            Assert.That(state.PossessedFormId, Is.EqualTo(form));
            Assert.That(state.PossessedBoostConfigurationId, Is.EqualTo(boost));
        });
    }

    [Test]
    public void WithCorruption_EnteringFormActivatesDefaultFormExactlyOnce()
    {
        var before = new DemonboundBattleState(8, 3);
        DemonboundBattleState entered = before.WithCorruption(10);

        Assert.Multiple(() =>
        {
            Assert.That(entered.Corruption, Is.EqualTo(10));
            Assert.That(entered.IsPossessed, Is.True);
            Assert.That(entered.PossessedFormId, Is.EqualTo(DemonboundBattleState.DefaultPossessedFormId));
            Assert.That(entered.PossessedBoostConfigurationId,
                Is.EqualTo(DemonboundBattleState.DefaultPossessedBoostConfigurationId));
            Assert.That(entered.PossessedBoostApplied, Is.False);
        });

        DemonboundBattleState replayed = entered.WithCorruption(10);
        Assert.Multiple(() =>
        {
            Assert.That(replayed.IsPossessed, Is.True);
            Assert.That(replayed.PossessedFormId, Is.EqualTo(entered.PossessedFormId));
            Assert.That(replayed.PossessedBoostConfigurationId, Is.EqualTo(entered.PossessedBoostConfigurationId));
            Assert.That(replayed.PossessedBoostApplied, Is.False);
        });
    }

    [Test]
    public void WithCorruption_NeverRemovesAnAlreadyEnteredForm()
    {
        var state = new DemonboundBattleState(10, 3, isPossessed: true);
        DemonboundBattleState lowered = state.WithCorruption(4);

        Assert.Multiple(() =>
        {
            Assert.That(lowered.Corruption, Is.EqualTo(4));
            Assert.That(lowered.IsPossessed, Is.True);
            Assert.That(lowered.PossessedFormId, Is.EqualTo(DemonboundBattleState.DefaultPossessedFormId));
        });
    }

    [Test]
    public void WithPossessedBoostApplied_IsIdempotent()
    {
        DemonboundBattleState state = new DemonboundBattleState(10, 3, isPossessed: true)
            .WithPossessedBoostApplied();

        Assert.That(state.PossessedBoostApplied, Is.True);
        Assert.That(state.WithPossessedBoostApplied().PossessedBoostApplied, Is.True);
        Assert.That(state.WithPossessedBoostApplied().PossessedFormId,
            Is.EqualTo(DemonboundBattleState.DefaultPossessedFormId));
    }

    [Test]
    public void BoostAppliedWithoutPossessedForm_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new DemonboundBattleState(10, 3, possessedBoostApplied: true));
    }

    [Test]
    public void TurnFlagsAndMindfulnessPreserveFormProjection()
    {
        DemonboundBattleState state = new DemonboundBattleState(10, 3, isPossessed: true)
            .WithPossessedBoostApplied();

        DemonboundBattleState meditated = state.WithMeditationUsed();
        DemonboundBattleState attacked = state.WithBasicAttackUsed();
        DemonboundBattleState skilled = state.WithNonMeditationSkillUsed();
        DemonboundBattleState mindful = state.WithMindfulnessLevel(2);
        DemonboundBattleState prepared = state.PrepareForTurn();

        Assert.Multiple(() =>
        {
            Assert.That(meditated.PossessedFormId, Is.EqualTo(state.PossessedFormId));
            Assert.That(meditated.PossessedBoostApplied, Is.True);
            Assert.That(attacked.PossessedFormId, Is.EqualTo(state.PossessedFormId));
            Assert.That(skilled.PossessedBoostApplied, Is.True);
            Assert.That(mindful.MindfulnessLevel, Is.EqualTo(2));
            Assert.That(mindful.PossessedFormId, Is.EqualTo(state.PossessedFormId));
            Assert.That(prepared.IsPossessed, Is.True);
            Assert.That(prepared.PossessedBoostApplied, Is.True);
        });
    }
}