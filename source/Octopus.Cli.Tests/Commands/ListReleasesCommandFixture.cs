﻿using System;
using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Client.Model;

namespace Octopus.Cli.Tests.Commands
{
    [TestFixture]
    public class ListReleasesCommandFixture : ApiCommandFixtureBase
    {
        ListReleasesCommand listReleasesCommand;

        [SetUp]
        public void SetUp()
        {
            listReleasesCommand = new ListReleasesCommand(RepositoryFactory, Log, FileSystem);
        }

        [Test]
        public void ShouldGetListOfReleases()
        {
            Repository.Projects.FindByNames(Arg.Any<IEnumerable<string>>()).Returns(new List<ProjectResource>
                {
                    new ProjectResource {Name = "ProjectA", Id = "projectaid"},
                    new ProjectResource {Name = "ProjectB", Id = "projectbid"}
                });

            Repository.Releases.FindMany(Arg.Any<Func<ReleaseResource, bool>>()).Returns(new List<ReleaseResource>
                {
                    new ReleaseResource {ProjectId = "projectaid", Version="1.0", Assembled = DateTimeOffset.MinValue, ReleaseNotes = "Release Notes 1"},
                    new ReleaseResource {ProjectId = "projectaid", Version="2.0", Assembled = DateTimeOffset.MaxValue, ReleaseNotes = "Release Notes 2"}
                });

            CommandLineArgs.Add("--project=ProjectA");

            listReleasesCommand.Execute(CommandLineArgs.ToArray());

            Log.Received().InfoFormat("Releases: {0}", 2);
            Log.Received().InfoFormat(" - Project: {0}", "ProjectA");
            Log.Received().InfoFormat("    {0}", "Version: 1.0");
            Log.Received().InfoFormat("    {0}", "Assembled: " + DateTimeOffset.MinValue);
            Log.Received().InfoFormat("    {0}", "Package Versions: ");
            Log.Received().InfoFormat("    {0}", "Release Notes: Release Notes 1");
            Log.Received().InfoFormat("    {0}", "Version: 2.0");
            Log.Received().InfoFormat("    {0}", "Assembled: " + DateTimeOffset.MaxValue);
            Log.Received().InfoFormat("    {0}", "Package Versions: ");
            Log.Received().InfoFormat("    {0}", "Release Notes: Release Notes 2");
        }
    }
}
