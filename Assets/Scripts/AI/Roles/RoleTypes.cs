public enum RoleType
{
    /// <summary>
    /// The default role type. This is used for roles that do not fit into any other category.
    /// </summary>
    Default,
    /// <summary>
    /// The role type that defines a "job". This is what you think of when you think of a role. Like "guard" or "merchant" or "pet".
    /// </summary>
    Job,
    /// <summary>
    /// The role type for NPCs that are unique in a level and have special abilities.
    /// </summary>
    UniqueIdentifier,
    /// <summary>
    /// The role type used to identify which NPCs are allowed in which areas. Interacts with Zone game objects to determine location based suspicion levels.
    /// </summary>
    ZoneAccess,
    /// <summary>
    /// The primary role type used to identify whether an NPC should be able to use a specific Interaction Definition.
    /// This may also depend on unique identifiers and item abilities.
    /// </summary>
    Skill,
    /// <summary>
    /// The role type used to identify whether an NPC should be able to take specific actions based on their possession of an item.
    /// </summary>
    ItemAbility,
    /// <summary>
    /// The role type used to identify whether a player has completed a specific task or quest.
    /// </summary>
    StoryFlag,
    /// <summary>
    /// Used for preventing player from performing interactions that the parasite doesn't know about.
    /// Usually shared between all NPCs.
    /// </summary>
    Knowledge,

}
