

public class CuriousReactionGraphConfiguration : AbstractGraphFactoryConfig
{
    
}

public class CuriousReactionGraphFactory : GenericAbstractGraphFactory<CuriousReactionGraphConfiguration>
{
    public CuriousReactionGraphFactory(CuriousReactionGraphConfiguration configuration, string graphId = null) : base(configuration, graphId)
    {
    }

    protected override void ConstructGraphInternal(StateGraph graph)
    {
        
    }
}