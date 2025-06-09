using System.Collections.Generic;
using System;

public interface IRoleProvider
{
    public IReadOnlyCollection<NpcRoleSO> GetCurrentRoles();

    public bool ShouldProvideRoles(NpcContext npcContext)
    {
        return true;
    }
    
    public event Action<NpcRoleSO> OnRoleAdded;
    public event Action<NpcRoleSO> OnRoleRemoved;
}
