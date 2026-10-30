using MakanApp.Domain.Organization;
using System.Security.Cryptography;
using System.Text;

namespace MakanApp.Application.Organization;

public static class WorkspaceFactory
{
    public static WorkspaceResult CreatePersonal() =>
        new(
            WorkspaceType.Personal,
            null,
            null,
            null,
            null,
            "فضای شخصی",
            "Active");

    public static WorkspaceResult CreateOrganization(OrganizationWorkspaceRecord record) =>
        new(
            WorkspaceType.Organization,
            record.OrganizationId,
            record.OrganizationName,
            record.MembershipId,
            record.Role,
            $"{record.OrganizationName} - {record.Role}",
            record.IsEffective ? "Active" : record.HasUnresolvedVisibility ? "Suspended" : "Ended")
        {
            MembershipStatus = record.MembershipStatus,
            RoleStatus = record.RoleStatus,
            OrganizationStatus = record.OrganizationStatus,
            IsEffective = record.IsEffective,
            CanEnter = record.IsEffective,
            IsVisible = record.IsEffective
        };

    public static WorkspaceSnapshotResult CreateSnapshot(IReadOnlyCollection<OrganizationWorkspaceRecord> records)
    {
        var effective = records.Any(r => r.IsEffective);
        bool? visible = effective ? false : records.Any(r => r.HasUnresolvedVisibility) ? null : true;
        var items = new List<WorkspaceResult>
        {
            CreatePersonal() with { IsVisible = visible, HasEffectiveEducationalRole = effective }
        };
        items.AddRange(records.Select(r => CreateOrganization(r) with { HasEffectiveEducationalRole = effective }));
        // نسخه فقط از واقعیت‌های مجوز ساخته می‌شود؛ پنهان‌شدن کارت، نشست یا دارایی شخصی را تغییر نمی‌دهد.
        var fingerprint = string.Join('|', records.OrderBy(r => r.OrganizationId).ThenBy(r => r.MembershipId).ThenBy(r => r.Role)
            .Select(r => $"{r.OrganizationId:N}:{r.MembershipId:N}:{r.Role}:{r.MembershipStatus}:{r.RoleStatus}:{r.OrganizationStatus}:{r.MembershipEndedAtUtc:O}:{r.RoleEndedAtUtc:O}"));
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))), effective, visible,
            visible is null ? "ProductDecisionRequired" : visible.Value ? "Visible" : "Hidden", items);
    }
}
