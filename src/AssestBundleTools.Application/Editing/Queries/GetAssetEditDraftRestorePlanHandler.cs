using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Editing.Queries;

public sealed class GetAssetEditDraftRestorePlanHandler
    : IQueryHandler<GetAssetEditDraftRestorePlanQuery, Result<AssetEditDraftRestorePlanDto>>
{
    private readonly IAssetEditDraftStore _draftStore;

    public GetAssetEditDraftRestorePlanHandler(IAssetEditDraftStore draftStore)
    {
        _draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
    }

    public Task<Result<AssetEditDraftRestorePlanDto>> HandleAsync(
        GetAssetEditDraftRestorePlanQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetEditDraftRestorePlanDto>.Cancelled());
        }

        var workspaceKey = NormalizeRequired(query.WorkspaceKey);
        if (string.IsNullOrEmpty(workspaceKey))
        {
            return Task.FromResult(Result<AssetEditDraftRestorePlanDto>.Failure(
                ApplicationErrors.AssetEditDraftWorkspaceKeyRequired()));
        }

        var nowUtc = query.CurrentTimeUtc ?? DateTimeOffset.UtcNow;
        var candidates = new List<AssetEditDraftRestoreCandidateDto>();
        foreach (var reference in query.DraftReferences ?? Array.Empty<AssetEditDraftReferenceDto>())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result<AssetEditDraftRestorePlanDto>.Cancelled());
            }

            candidates.Add(ResolveCandidate(workspaceKey, reference, nowUtc));
        }

        var plan = new AssetEditDraftRestorePlanDto(
            WorkspaceKey: workspaceKey,
            TotalReferenceCount: candidates.Count,
            RestorableCount: Count(candidates, AssetEditDraftRestoreStatus.Restorable),
            MissingCount: Count(candidates, AssetEditDraftRestoreStatus.Missing),
            SourceMismatchCount: Count(candidates, AssetEditDraftRestoreStatus.SourceMismatch),
            ExpiredCount: Count(candidates, AssetEditDraftRestoreStatus.Expired),
            InvalidReferenceCount: Count(candidates, AssetEditDraftRestoreStatus.InvalidReference),
            Candidates: candidates);

        return Task.FromResult(Result<AssetEditDraftRestorePlanDto>.Success(plan));
    }

    private AssetEditDraftRestoreCandidateDto ResolveCandidate(
        string workspaceKey,
        AssetEditDraftReferenceDto? reference,
        DateTimeOffset nowUtc)
    {
        if (reference is null)
        {
            return CreateInvalidCandidate(
                new AssetEditDraftReferenceDto(string.Empty, string.Empty),
                ApplicationErrors.AssetEditDraftReferenceRequired());
        }

        var normalizedReference = NormalizeReference(reference);
        var validationError = ValidateReference(normalizedReference);
        if (validationError is not null)
        {
            return CreateInvalidCandidate(normalizedReference, validationError);
        }

        var lookupKey = new AssetEditDraftLookupKey(
            workspaceKey,
            normalizedReference.DraftId,
            normalizedReference.DraftMode);
        var descriptor = _draftStore.Load(lookupKey);
        if (descriptor is null)
        {
            return new AssetEditDraftRestoreCandidateDto(
                normalizedReference,
                AssetEditDraftRestoreStatus.Missing,
                Diagnostic: ApplicationErrors.AssetEditDraftNotFound(
                    workspaceKey,
                    normalizedReference.DraftId,
                    normalizedReference.DraftMode));
        }

        if (HasSourceMismatch(normalizedReference, descriptor))
        {
            return new AssetEditDraftRestoreCandidateDto(
                normalizedReference,
                AssetEditDraftRestoreStatus.SourceMismatch,
                descriptor,
                ApplicationErrors.AssetEditDraftSourceMismatch(
                    workspaceKey,
                    normalizedReference.DraftId,
                    normalizedReference.DraftMode));
        }

        if (descriptor.ExpiresAtUtc.HasValue && descriptor.ExpiresAtUtc.Value <= nowUtc)
        {
            return new AssetEditDraftRestoreCandidateDto(
                normalizedReference,
                AssetEditDraftRestoreStatus.Expired,
                descriptor,
                ApplicationErrors.AssetEditDraftExpired(
                    workspaceKey,
                    normalizedReference.DraftId,
                    normalizedReference.DraftMode));
        }

        return new AssetEditDraftRestoreCandidateDto(
            normalizedReference,
            AssetEditDraftRestoreStatus.Restorable,
            descriptor);
    }

    private static AssetEditDraftRestoreCandidateDto CreateInvalidCandidate(
        AssetEditDraftReferenceDto reference,
        AppError diagnostic) =>
        new(
            reference,
            AssetEditDraftRestoreStatus.InvalidReference,
            Diagnostic: diagnostic);

    private static AppError? ValidateReference(AssetEditDraftReferenceDto reference)
    {
        if (string.IsNullOrEmpty(reference.DraftId))
        {
            return ApplicationErrors.AssetEditDraftIdRequired();
        }

        return string.IsNullOrEmpty(reference.DraftMode)
            ? ApplicationErrors.AssetEditDraftModeRequired()
            : null;
    }

    private static bool HasSourceMismatch(
        AssetEditDraftReferenceDto reference,
        AssetEditDraftDescriptorDto descriptor)
    {
        if (string.IsNullOrWhiteSpace(reference.SourceFingerprint))
        {
            return false;
        }

        return !string.Equals(
            reference.SourceFingerprint,
            NormalizeOptional(descriptor.SourceFingerprint),
            StringComparison.Ordinal);
    }

    private static AssetEditDraftReferenceDto NormalizeReference(AssetEditDraftReferenceDto reference) =>
        new(
            NormalizeRequired(reference.DraftId),
            NormalizeRequired(reference.DraftMode),
            NormalizeOptional(reference.SourceFingerprint),
            NormalizeOptional(reference.SourceDescription));

    private static string NormalizeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static int Count(
        IReadOnlyList<AssetEditDraftRestoreCandidateDto> candidates,
        AssetEditDraftRestoreStatus status) =>
        candidates.Count(candidate => candidate.Status == status);
}
