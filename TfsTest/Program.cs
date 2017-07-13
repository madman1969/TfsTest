namespace TfsTool
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;

  using Fclp;

  using Microsoft.TeamFoundation.Build.WebApi;
  using Microsoft.TeamFoundation.Core.WebApi;
  using Microsoft.VisualStudio.Services.Client;
  using Microsoft.VisualStudio.Services.WebApi;

  using Newtonsoft.Json;

  using Tababular;

  static class Program
  {
    private const string TeamProjectName = "ImagoPortal";
    private const string TfsUrl = @"http://tfs.dthomas.co.uk:8080/tfs/ImagoBOCollection";
    private static BuildHttpClient buildClient;

    private static ApplicationArguments applicationArguments;

    static void Main(string[] args)
    {
      var hasErrors = false;

      // Assume ImagoPortal and current user credentials ...
      buildClient = new BuildHttpClient(new Uri(TfsUrl), new VssAadCredential());

      // Parse the command line args ...
      applicationArguments = ParseArgs(args, out hasErrors);

      // Display usage if requested, or on error ...
      if (hasErrors || applicationArguments.Help)
      {
        ShowUsage();
        return;
      }

      // Trigger build if build definition id supplied ...
      if (applicationArguments.DefinitionId != -1)
      {
        TriggerBuild(applicationArguments.DefinitionId);
        return;
      }

      // Just display build statuses ...
      DisplayBuildDefinitionStatuses();

    }

    /// <summary>
    /// Displays a usage summary.
    /// </summary>
    private static void ShowUsage()
    {
      Console.WriteLine("Displays list of build definitions and statuses");
      Console.WriteLine("\nSupported options:\n");
      Console.WriteLine("  /d [build definition id]\t- Queue build with specified definition id");
      Console.WriteLine("  /?\t\t\t\t- Display this message");
      Console.WriteLine("  /c\t\t\t\t- Display count of build definitions");
      Console.WriteLine("  /j\t\t\t\t- Output as JSON");
    }

    /// <summary>
    /// Parses the command line arguments.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="hasErrors">if set to <c>true</c> [has errors].</param>
    /// <returns></returns>
    private static ApplicationArguments ParseArgs(string[] args, out bool hasErrors)
    {
      // create a generic parser for the ApplicationArguments type
      var p = new FluentCommandLineParser<ApplicationArguments>();

      p.Setup(arg => arg.DefinitionId).As('d', "definitionid").SetDefault(-1);

      p.Setup(arg => arg.Help).As('?', "help").SetDefault(false);

      p.Setup(arg => arg.AsJson).As('j', "asjson").SetDefault(false);

      p.Setup(arg => arg.AsCount).As('c', "ascount").SetDefault(false);

      var result = p.Parse(args);
      hasErrors = result.HasErrors;

      return p.Object;
    }

    /// <summary>
    /// Triggers a new build for the specified build definition.
    /// </summary>
    /// <param name="definitionId">The definition identifier.</param>
    private static void TriggerBuild(int definitionId)
    {
      try
      {
        var definitionReference = new DefinitionReference
        {
          Id = definitionId,
          Project = new TeamProjectReference
          {
            Name = TeamProjectName
          }
        };

        var build = new Build { Definition = definitionReference };

        // Trigger the build ...
        var buildnumber = buildClient.QueueBuildAsync(build, TeamProjectName).Result;

        Console.WriteLine("Build Triggered ...\n");

        var buildInfoList = new List<BuildInfo>
                        {
                          new BuildInfo
                            {
                              Name = buildnumber.Definition.Name,
                              DefinitionId = buildnumber.Definition.Id.ToString(),
                              Id = buildnumber.Id.ToString(),
                              Status = buildnumber.Status.ToString(),
                              Result = buildnumber.Result?.ToString() ?? "N/A",
                              StartTime = DateTime.Now.ToString(CultureInfo.CurrentCulture)
                            }
                        };

        DisplayBuildInfoTable(buildInfoList);
      }
      catch (Exception)
      {
        var stringoutput = $"Error: Failed to trigger build";
        Console.WriteLine(stringoutput);
      }
    }

    /// <summary>
    /// Displays the build definition statuses as a table.
    /// </summary>
    private static void DisplayBuildDefinitionStatuses()
    {
      // Get distinct list of build definitions/statuses ...
      var builds = buildClient.GetBuildsAsync(TeamProjectName)
                              .SyncResult()
                              .GroupBy(x => x.Definition.Name)
                              .Select(g => g.First())
                              .OrderBy(x => x.Definition.Name)
                              .ToList();

      // Just display count of builds if requested ...
      if (applicationArguments.AsCount)
      {
        Console.WriteLine($"Found [{builds.Count()}] build definitions\n");
        return;
      }

      var buildInfoList = new List<BuildInfo>();

      foreach (var build in builds)
      {
        var tmp = new BuildInfo
        {
          Name = build.Definition.Name,
          DefinitionId = build.Definition.Id.ToString(),
          Id = build.Id.ToString(),
          Status = build.Status.ToString(),
          Result =  build.Result?.ToString() ?? "N/A",
          StartTime = build.StartTime?.ToLocalTime().ToString(CultureInfo.CurrentCulture) ?? @"Unspecified"
        };


        buildInfoList.Add(tmp);
      }

      // Output as JSON as requested ...
      if (applicationArguments.AsJson)
      {
        var json = JsonConvert.SerializeObject(buildInfoList);
        Console.WriteLine(json);
        return;
      }


      DisplayBuildInfoTable(buildInfoList);

    }

    /// <summary>
    /// Displays the supplied list of build information as a nicely formatted table.
    /// </summary>
    /// <param name="buildInfoList">The build information list.</param>
    private static void DisplayBuildInfoTable(List<BuildInfo> buildInfoList)
    {
      // The following outputs the build details in a nice table ...
      var formatter = new TableFormatter();
      var text = formatter.FormatObjects(buildInfoList);

      Console.WriteLine(text);
    }
  }
}
