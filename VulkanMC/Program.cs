namespace VulkanMC;

class Program
{
    static void Main(string[] args)
    {
        using var engine = new VulkanEngine();
        engine.Run();
    }
}
