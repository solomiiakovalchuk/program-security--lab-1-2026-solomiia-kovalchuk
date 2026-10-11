using Reports.Core.Identity;
using Reports.Core.Identity.Abstractions;
using Reports.Core.Identity.Exceptions;

namespace Reports.Identity;

/// <summary>Role-to-action access matrix; actions not listed for a role are denied.</summary>
public sealed class AccessPolicyService : IAccessPolicy
{
    private static readonly HashSet<string> OperatorActions = new(StringComparer.Ordinal)
    {
        "GenerateReport",
        "ViewJournal",
        "ViewResponses",
        "Sync",
        "CancelRecord",
        "RestoreRecord",
        "ChangeOwnPassword"
    };

    private static readonly HashSet<string> AdministratorActions = new(OperatorActions, StringComparer.Ordinal)
    {
        "EditReferenceData",
        "EditPeople",
        "PublishToGoogle",
        "ChangeSettings",
        "RebuildTemplate",
        "ForgetToken",
        "ManageAccounts",
        "ViewAuditLog",
        "ExportAuditLog"
    };

    private static readonly Dictionary<Role, HashSet<string>> Matrix = new()
    {
        [Role.Operator] = OperatorActions,
        [Role.Administrator] = AdministratorActions
    };

    /// <inheritdoc/>
    public bool IsAllowed(Role role, string action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Matrix.TryGetValue(role, out var actions) && actions.Contains(action);
    }

    /// <inheritdoc/>
    public void Demand(Role role, string action)
    {
        if (!IsAllowed(role, action))
        {
            throw new AccessDeniedException($"Role '{role}' is not allowed to perform '{action}'.");
        }
    }
}
