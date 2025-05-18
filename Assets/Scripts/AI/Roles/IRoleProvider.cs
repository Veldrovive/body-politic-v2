using System.Collections.Generic;
using System;

public interface IRoleProvider
{
    public IReadOnlyCollection<NpcRoleSO> GetCurrentRoles();
    public event Action<NpcRoleSO> OnRoleAdded;
    public event Action<NpcRoleSO> OnRoleRemoved;
}
