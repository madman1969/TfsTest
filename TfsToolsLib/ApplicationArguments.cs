namespace TfsToolsLib
{
  public class ApplicationArguments
  {
    public int DefinitionId { get; set; }

    public bool Help { get; set; }

    public bool AsJson { get; set; }

    public bool AsCount { get; set; }

    public string ProjectName { get; set; }

    public string TfsUrl { get; set; }
  }
}
