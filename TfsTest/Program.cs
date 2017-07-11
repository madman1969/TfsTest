namespace TfsTest
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Net;

  using Microsoft.TeamFoundation.Build.Client;
  using Microsoft.TeamFoundation.Build.WebApi;
  using Microsoft.TeamFoundation.Client;
  using Microsoft.TeamFoundation.VersionControl.Client;
  using Microsoft.VisualStudio.Services.Client;
  using Microsoft.VisualStudio.Services.WebApi;

  using Tababular;

  using BuildQueryOrder = Microsoft.TeamFoundation.Build.Client.BuildQueryOrder;

  static class Program
  {
    static void Main(string[] args)
    {
      GetBuildStatus();
    }

    private static void GetBuildStatus()
    {
      const string TfsUrl = @"http://tfs.dthomas.co.uk:8080/tfs/ImagoBOCollection";
      var buildClient = new BuildHttpClient(new Uri(TfsUrl), new VssAadCredential());
      var definitions = buildClient.GetDefinitionsAsync(project: "ImagoPortal");



      var builds = buildClient.GetBuildsAsync("ImagoPortal")
                              .SyncResult()
                              .GroupBy(x => x.Definition.Name)
                              .Select(g => g.First())
                              .OrderBy(x => x.Definition.Name)
                              .ToList();


      Console.WriteLine($"Found {builds.Count()} builds\n");

      var formatter = new TableFormatter();

      var objects = new List<BuildInfo>();

      foreach (var build in builds)
      {
        var tmp = new BuildInfo
                    {
                      BuildName = build.Definition.Name,
                      BuildId = build.Id.ToString(),
                      BuildStatus = build.Status.ToString(),
                      BuildStartTime = build.StartTime.ToString()
                    };

        objects.Add(tmp);
      }

      var text = formatter.FormatObjects(objects);
      Console.WriteLine(text);

      Console.WriteLine("\nPress any key");
      Console.ReadLine();
    }

    #region XAML build details

    // Retrieve list of builds ...
    //var builds = buildClient.GetBuildsAsync("ImagoPortal")
    //                        .SyncResult()
    //                        .OrderBy(x => x.Definition.Name).
    //                        ThenByDescending(x => x.BuildNumber);

    // Console.WriteLine($"[{build.Definition.Name}] - {build.Id} - {build.Status} - {build.StartTime}");

    // Translate username and password to TFS Credentials
    //var tfsCredential = TfsClientCredentials;

    //var tfsUri = new Uri(@"http://tfs.dthomas.co.uk:8080/tfs/ImagoBOCollection");
    //var tfs = new TfsTeamProjectCollection(tfsUri, tfsCredential);


    //var vcs = tfs.GetService<VersionControlServer>();

    //var teamProjects = vcs.GetAllTeamProjects(true);

    //var buildServer = (IBuildServer)tfs.GetService(typeof(IBuildServer));

    // SlowBuildDefsByProject(teamProjects, buildServer);
    // FastBuildsDefsByProject(teamProjects, buildServer);
    // ListAllBuildDefs(buildServer);

    private static void ListAllBuildDefs(IBuildServer buildServer)
    {
      var buildDefinitions = buildServer.QueryBuildDefinitions("ImagoPortal");

      foreach (var buildDefinition in buildDefinitions)
      {
        var result = $"Build Def [{buildDefinition.Name}]";
        Console.WriteLine(result);
      }

      Console.WriteLine("Press any key");
      Console.ReadLine();
    }

    private static void SlowBuildDefsByProject(TeamProject[] teamProjects, IBuildServer buildServer)
    {
      foreach (var proj in teamProjects)
      {
        var builds = buildServer.QueryBuilds(proj.Name);

        foreach (var build in builds)
        {
          var result = string.Format("Build {0}/{3} {4} - current status {1} - as of {2}",
            build.BuildDefinition.Name,
            build.Status,
            build.FinishTime,
            build.LabelName,
            Environment.NewLine);

          Console.WriteLine(result);
        }
      }

      Console.ReadLine();
    }

    private static void FastBuildsDefsByProject(TeamProject[] teamProjects, IBuildServer buildServer)
    {
      foreach (var proj in teamProjects)
      {
        var defs = buildServer.QueryBuildDefinitions(proj.Name);

        Console.WriteLine($"Team Project: {proj.Name}");

        foreach (var def in defs)
        {
          var spec = buildServer.CreateBuildDetailSpec(proj.Name, def.Name);
          spec.MaxBuildsPerDefinition = 1;
          spec.QueryOrder = BuildQueryOrder.FinishTimeDescending;

          var builds = buildServer.QueryBuilds(spec);

          if (builds.Builds.Length > 0)
          {
            var buildDetail = builds.Builds[0];

            Console.WriteLine($"   {def.Name} - {buildDetail.Status} - {buildDetail.FinishTime}");
          }
        }

        Console.WriteLine();
      }

      Console.WriteLine("Press any key");
      Console.ReadLine();
    }

    private static TfsClientCredentials TfsClientCredentials
    {
      get
      {
        ICredentials networkCredential = new NetworkCredential("aross", "Zard05!53", "DT");
        var windowsCredential = new WindowsCredential(networkCredential);
        var tfsCredential = new TfsClientCredentials(windowsCredential, false);
        return tfsCredential;
      }
    }

    #endregion
  }
}
