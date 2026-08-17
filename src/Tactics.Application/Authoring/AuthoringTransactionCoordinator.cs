namespace Tactics.Application.Authoring;

public interface IAuthoringTransactionParticipant
{
    string Identity { get; }
    void Prepare();
    void Apply();
    void Rollback();
}

public sealed record AuthoringTransactionResult(bool Succeeded, IReadOnlyList<string> Applied, Exception? Error = null);

/// <summary>Coordinates an ordered authoring batch and rolls every prepared participant back on failure.</summary>
public sealed class AuthoringTransactionCoordinator
{
    public AuthoringTransactionResult Execute(IEnumerable<IAuthoringTransactionParticipant> participants)
    {
        IAuthoringTransactionParticipant[] values = (participants ?? throw new ArgumentNullException(nameof(participants))).ToArray();
        if (values.Length == 0 || values.Select(value => value.Identity).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Authoring transactions require unique non-empty participants.", nameof(participants));
        var prepared = new List<IAuthoringTransactionParticipant>();
        var applied = new List<string>();
        try
        {
            foreach (IAuthoringTransactionParticipant participant in values)
            {
                if (string.IsNullOrWhiteSpace(participant.Identity)) throw new ArgumentException("Participant identity is required.");
                participant.Prepare(); prepared.Add(participant);
            }
            foreach (IAuthoringTransactionParticipant participant in values) { participant.Apply(); applied.Add(participant.Identity); }
            return new AuthoringTransactionResult(true, Array.AsReadOnly(applied.ToArray()));
        }
        catch (Exception error)
        {
            List<Exception> rollbackErrors = [];
            foreach (IAuthoringTransactionParticipant participant in prepared.AsEnumerable().Reverse())
                try { participant.Rollback(); } catch (Exception rollbackError) { rollbackErrors.Add(rollbackError); }
            Exception resultError = rollbackErrors.Count == 0 ? error : new AggregateException("Authoring apply and rollback both failed.", new[] { error }.Concat(rollbackErrors));
            return new AuthoringTransactionResult(false, Array.AsReadOnly(applied.ToArray()), resultError);
        }
    }
}
