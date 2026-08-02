using HackerOs.App.Abstractions;
using HackerOs.Simulation.Abstractions.Sessions;

namespace HackerOs.Platform.Core.Sessions;

/// <summary>In-memory <see cref="ILocalUserRepository"/> enforcing the ADR 0013 last-administrator rule.</summary>
public sealed class InMemoryLocalUserRepository : ILocalUserRepository
{
    private readonly Lock _gate = new();
    private readonly Dictionary<LocalUserId, LocalUser> _usersById = [];
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>Initializes an in-memory user repository.</summary>
    /// <param name="clock">Clock used for created/updated timestamps; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    public InMemoryLocalUserRepository(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public LocalUser CreateUser(
        LocalLoginName loginName,
        string displayName,
        AppAuthority authority,
        LocalGroupId primaryGroupId,
        IReadOnlyCollection<LocalGroupId>? additionalGroupIds = null,
        LocalPasswordCredential? credential = null)
    {
        lock (_gate)
        {
            if (_usersById.Values.Any(existing => existing.LoginName == loginName))
            {
                throw new InvalidOperationException($"Login name '{loginName}' is already in use.");
            }

            DateTimeOffset now = _clock();
            LocalUser user = new(
                LocalUserId.FromGuid(Guid.NewGuid()),
                loginName,
                displayName,
                enabled: true,
                authority,
                primaryGroupId,
                additionalGroupIds ?? [],
                credential,
                revision: 1,
                now,
                now);

            _usersById[user.Id] = user;
            return user;
        }
    }

    /// <inheritdoc />
    public ValueTask<LocalUser> CreateUserAsync(
        LocalLoginName loginName,
        string displayName,
        AppAuthority authority,
        LocalGroupId primaryGroupId,
        IReadOnlyCollection<LocalGroupId>? additionalGroupIds = null,
        LocalPasswordCredential? credential = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CreateUser(
            loginName, displayName, authority, primaryGroupId, additionalGroupIds, credential));
    }

    /// <inheritdoc />
    public bool TryGetById(LocalUserId id, out LocalUser user)
    {
        lock (_gate)
        {
            return _usersById.TryGetValue(id, out user!);
        }
    }

    /// <inheritdoc />
    public ValueTask<LocalUser?> FindByIdAsync(
        LocalUserId id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(TryGetById(id, out LocalUser? user) ? user : null);
    }

    /// <inheritdoc />
    public bool TryGetByLoginName(LocalLoginName loginName, out LocalUser user)
    {
        lock (_gate)
        {
            user = _usersById.Values.FirstOrDefault(existing => existing.LoginName == loginName)!;
            return user is not null;
        }
    }

    /// <inheritdoc />
    public ValueTask<LocalUser?> FindByLoginNameAsync(
        LocalLoginName loginName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(TryGetByLoginName(loginName, out LocalUser? user) ? user : null);
    }

    /// <inheritdoc />
    public IReadOnlyList<LocalUser> GetAll()
    {
        lock (_gate)
        {
            return [.. _usersById.Values];
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LocalUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GetAll());
    }

    /// <inheritdoc />
    public LocalUser SetEnabled(LocalUserId id, bool enabled)
    {
        lock (_gate)
        {
            LocalUser user = GetRequired(id);
            if (!enabled && IsLastEnabledAdministrator(user))
            {
                throw new InvalidOperationException("Cannot disable the last enabled Administrator.");
            }

            LocalUser updated = WithMutation(user, enabled: enabled);
            _usersById[id] = updated;
            return updated;
        }
    }

    /// <inheritdoc />
    public ValueTask<LocalUser> SetEnabledAsync(
        LocalUserId id,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SetEnabled(id, enabled));
    }

    /// <inheritdoc />
    public LocalUser SetAuthority(LocalUserId id, AppAuthority authority)
    {
        lock (_gate)
        {
            LocalUser user = GetRequired(id);
            if (authority != AppAuthority.Administrator && IsLastEnabledAdministrator(user))
            {
                throw new InvalidOperationException("Cannot demote the last enabled Administrator.");
            }

            LocalUser updated = WithMutation(user, authority: authority);
            _usersById[id] = updated;
            return updated;
        }
    }

    /// <inheritdoc />
    public ValueTask<LocalUser> SetAuthorityAsync(
        LocalUserId id,
        AppAuthority authority,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SetAuthority(id, authority));
    }

    private bool IsLastEnabledAdministrator(LocalUser user) =>
        user.Enabled
        && user.Authority == AppAuthority.Administrator
        && _usersById.Values.Count(existing => existing.Enabled && existing.Authority == AppAuthority.Administrator) <= 1;

    private LocalUser GetRequired(LocalUserId id) => _usersById.TryGetValue(id, out LocalUser? user)
        ? user
        : throw new KeyNotFoundException($"No local user with ID '{id}'.");

    private LocalUser WithMutation(LocalUser user, bool? enabled = null, AppAuthority? authority = null) => new(
        user.Id,
        user.LoginName,
        user.DisplayName,
        enabled ?? user.Enabled,
        authority ?? user.Authority,
        user.PrimaryGroupId,
        user.AdditionalGroupIds,
        user.Credential,
        user.Revision + 1,
        user.CreatedAtUtc,
        _clock());
}

/// <summary>In-memory <see cref="ILocalGroupRepository"/>.</summary>
public sealed class InMemoryLocalGroupRepository : ILocalGroupRepository
{
    private readonly Lock _gate = new();
    private readonly Dictionary<LocalGroupId, LocalGroup> _groupsById = [];

    /// <inheritdoc />
    public LocalGroup CreateGroup(LocalLoginName name)
    {
        lock (_gate)
        {
            LocalGroup group = new(LocalGroupId.FromGuid(Guid.NewGuid()), name);
            _groupsById[group.Id] = group;
            return group;
        }
    }

    /// <inheritdoc />
    public ValueTask<LocalGroup> CreateGroupAsync(
        LocalLoginName name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CreateGroup(name));
    }

    /// <inheritdoc />
    public bool TryGetById(LocalGroupId id, out LocalGroup group)
    {
        lock (_gate)
        {
            return _groupsById.TryGetValue(id, out group!);
        }
    }

    /// <inheritdoc />
    public ValueTask<LocalGroup?> FindByIdAsync(
        LocalGroupId id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(TryGetById(id, out LocalGroup? group) ? group : null);
    }

    /// <inheritdoc />
    public ValueTask<LocalGroup?> FindByNameAsync(
        LocalLoginName name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult<LocalGroup?>(
                _groupsById.Values.FirstOrDefault(group => group.Name == name));
        }
    }
}
