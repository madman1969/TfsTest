
namespace TfsToolsLib
{
  using System;
  using System.Collections.Generic;
  using System.Drawing;
  using System.Globalization;
  using System.Linq;
  using System.Threading;

  using Colorful;

  using Fclp;

  using Microsoft.TeamFoundation.Build.WebApi;
  using Microsoft.TeamFoundation.Core.WebApi;
  using Microsoft.VisualStudio.Services.Client;
  using Microsoft.VisualStudio.Services.WebApi;

  using Newtonsoft.Json;

  using Tababular;

  using Console = Colorful.Console;

  public class Tfslib
  {

    #region Fields and properties

    private static BuildHttpClient buildClient;

    private static ApplicationArguments appArgs;

    #endregion

    #region Main Loop


    public Tfslib(string[] args)
    {
      bool hasErrors;


      // Parse the command line args ...
      appArgs = this.ParseArgs(args, out hasErrors);

      // Assume ImagoPortal and current user credentials ...
      try
      {
        buildClient = new BuildHttpClient(new Uri(appArgs.TfsUrl), new VssAadCredential());
      }
      catch
      {
        Console.WriteLine($"ERROR - Unable to connect to TFS URL [{appArgs.TfsUrl}]\n", Color.Red);
        hasErrors = true;
      }

      // Display usage if requested, or on error ...
      if (hasErrors || appArgs.Help)
      {
        this.ShowUsage();
        return;
      }

      // Trigger build if build definition id supplied ...
      if (appArgs.DefinitionId != -1)
      {
        this.TriggerBuild(appArgs.DefinitionId);
        return;
      }

      // No repeat specified ...
      if (!appArgs.Repeat)
      {
        // Just display build statuses ...
        this.DisplayBuildDefinitionStatuses();
      }
      // Repeatedly update the build statuses ...
      else
      {
        while (true)
        {
          Thread.Sleep(5000);

          this.DisplayBuildDefinitionStatuses();
        }
      }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Displays a usage summary.
    /// </summary>
    private void ShowUsage()
    {
      Console.WriteLine("Displays list of build definitions and statuses", Color.White);
      Console.WriteLine("\nSupported options:\n", Color.White);
      Console.WriteLine("  /?\t\t\t\t- Display this message", Color.White);
      Console.WriteLine("  /c\t\t\t\t- Display count of build definitions", Color.White);
      Console.WriteLine("  /d [build definition id]\t- Queue build with specified definition id", Color.White);
      Console.WriteLine("  /j\t\t\t\t- Output as JSON", Color.White);
      Console.WriteLine("  /p\t\t\t\t- Team Project Name", Color.White);
      Console.WriteLine("  /u\t\t\t\t- The TFS URL", Color.White);
      Console.WriteLine("  /r\t\t\t\t- Auto-update every 5 seconds", Color.White);
    }

    /// <summary>
    /// Parses the command line arguments.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="hasErrors">if set to <c>true</c> [has errors].</param>
    /// <returns></returns>
    private ApplicationArguments ParseArgs(string[] args, out bool hasErrors)
    {
      // create a generic parser for the ApplicationArguments type
      var p = new FluentCommandLineParser<ApplicationArguments>();

      p.Setup(arg => arg.DefinitionId).As('d', "definitionid").SetDefault(-1);

      p.Setup(arg => arg.Help).As('?', "help").SetDefault(false);

      p.Setup(arg => arg.AsJson).As('j', "asjson").SetDefault(false);

      p.Setup(arg => arg.AsCount).As('c', "ascount").SetDefault(false);

      p.Setup(arg => arg.ProjectName).As('p', "projectname").SetDefault("ImagoPortal");

      p.Setup(arg => arg.TfsUrl).As('u', "tfsurl").SetDefault(@"http://tfs.dthomas.co.uk:8080/tfs/ImagoBOCollection");

      p.Setup(arg => arg.Repeat).As('r', "repeat").SetDefault(false);

      var result = p.Parse(args);
      hasErrors = result.HasErrors;

      return p.Object;
    }

    /// <summary>
    /// Displays the supplied list of build information as a nicely formatted table.
    /// </summary>
    /// <param name="buildInfoList">The build information list.</param>
    private void DisplayBuildInfoTable(List<BuildInfo> buildInfoList)
    {
      var styleSheet = new StyleSheet(Color.White);
      styleSheet.AddStyle("Completed", Color.Chartreuse);
      styleSheet.AddStyle("Succeeded", Color.Chartreuse);
      styleSheet.AddStyle("N/A", Color.Orange);
      styleSheet.AddStyle("InProgress", Color.Orange);
      styleSheet.AddStyle("NotStarted", Color.Orange);
      styleSheet.AddStyle("Unspecified", Color.Orange);
      styleSheet.AddStyle("Canceled", Color.Orange);
      styleSheet.AddStyle("Failed", Color.Red);

      styleSheet.AddStyle("TFS2015 - [a-zA-Z0-9 ()]{1,}", Color.Aqua);



      // The following outputs the build details in a nice table ...
      var formatter = new TableFormatter();
      var text = formatter.FormatObjects(buildInfoList);

      // Console.WriteLine(text);
      Console.WriteLineStyled(text, styleSheet);
    }

    /// <summary>
    /// Displays the build definition statuses as a table.
    /// </summary>
    private void DisplayBuildDefinitionStatuses()
    {
      Console.Clear();

      var builds = new List<Build>();

      // Get distinct list of build definitions/statuses ...
      try
      {
        builds = buildClient.GetBuildsAsync(appArgs.ProjectName)
          .SyncResult()
          .GroupBy(x => x.Definition.Name)
          .Select(g => g.First())
          .OrderBy(x => x.Definition.Name)
          .ToList();
      }
      // !!! JUST SWALLOW THE EXCEPTION !!!
      catch
      {
        Console.WriteLine($"ERROR - Failed to retrieve build definitions for [{appArgs.ProjectName}]", Color.Red);
      }

      // Just display count of builds if requested ...
      if (appArgs.AsCount)
      {
        Console.WriteLine($"Found [{builds.Count}] build definitions\n", Color.White);
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
                      Result = build.Result?.ToString() ?? "N/A",
                      StartTime = build.StartTime?.ToLocalTime().ToString(CultureInfo.CurrentCulture) ?? @"Unspecified"
                    };


        buildInfoList.Add(tmp);
      }

      // Output as JSON as requested ...
      if (appArgs.AsJson)
      {
        var json = JsonConvert.SerializeObject(buildInfoList);
        Console.WriteLine(json);
        return;
      }

      this.DisplayBuildInfoTable(buildInfoList);
    }

    /// <summary>
    /// Triggers a new build for the specified build definition.
    /// </summary>
    /// <param name="definitionId">The definition identifier.</param>
    private void TriggerBuild(int definitionId)
    {
      try
      {
        var definitionReference = new DefinitionReference
                                    {
                                      Id = definitionId,
                                      Project = new TeamProjectReference
                                                  {
                                                    Name = appArgs.ProjectName
                                      }
                                    };

        var build = new Build { Definition = definitionReference };

        // Trigger the build ...
        var buildnumber = buildClient.QueueBuildAsync(build, appArgs.ProjectName).Result;

        Console.WriteLine("Build Triggered ...\n", Color.White);

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

        this.DisplayBuildInfoTable(buildInfoList);
      }
      catch (Exception)
      {
        var stringoutput = "Error: Failed to trigger build";
        Console.WriteLine(stringoutput, Color.Red);
      }
    }

    #endregion
  }
}
