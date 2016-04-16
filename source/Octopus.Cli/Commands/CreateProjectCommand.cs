﻿using System;
using log4net;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Octopus.Client.Model;

namespace Octopus.Cli.Commands
{
    [Command("create-project", Description = "Creates a project")]
    public class CreateProjectCommand : ApiCommand
    {
        public CreateProjectCommand(IOctopusRepositoryFactory repositoryFactory, ILog log, IOctopusFileSystem fileSystem)
            : base(repositoryFactory, log, fileSystem)
        {
            var options = Options.For("Project creation");
            options.Add("name=", "The name of the project", v => ProjectName = v);
            options.Add("projectGroup=", "The name of the project group to add this project to. If the group doesn't exist, it will be created.", v => ProjectGroupName = v);
            options.Add("lifecycle=", "The name of the lifecycle that the project will use.", v=> LifecycleName = v);
            options.Add("ignoreIfExists", "If the project already exists, an error will be returned. Set this flag to ignore the error.", v => IgnoreIfExists = true);
        }

        public string ProjectName { get; set; }
        public string ProjectGroupName { get; set; }
        public bool IgnoreIfExists { get; set; }
        public string LifecycleName { get; set; }

        protected override void Execute()
        {
            if (string.IsNullOrWhiteSpace(ProjectGroupName)) throw new CommandException("Please specify a project group name using the parameter: --projectGroup=XYZ");
            if (string.IsNullOrWhiteSpace(ProjectName)) throw new CommandException("Please specify a project name using the parameter: --name=XYZ");
            if (string.IsNullOrWhiteSpace(LifecycleName)) throw new CommandException("Please specify a lifecycle name using the parameter: --lifecycle=XYZ");

            Log.Info("Finding project group: " + ProjectGroupName);
            var group = Repository.ProjectGroups.FindByName(ProjectGroupName);
            if (group == null)
            {
                Log.Info("Project group does not exist, it will be created");
                group = Repository.ProjectGroups.Create(new ProjectGroupResource {Name = ProjectGroupName});
            }

            Log.Info("Finding lifecycle: " + LifecycleName);
            var lifecycle = Repository.Lifecycles.FindOne(l => l.Name.Equals(LifecycleName, StringComparison.OrdinalIgnoreCase));
            if (lifecycle == null)
                throw new CommandException("The lifecycle " + LifecycleName + " does not exist.");

            var project = Repository.Projects.FindByName(ProjectName);
            if (project != null)
            {
                if (IgnoreIfExists)
                {
                    Log.Info("The project " + project.Name + " (ID " + project.Id + ") already exists");
                    return;
                }

                throw new CommandException("The project " + project.Name + " (ID " + project.Id + ") already exists in this project group.");
            }
            
            Log.Info("Creating project: " + ProjectName);
            project = Repository.Projects.Create(new ProjectResource {Name = ProjectName, ProjectGroupId = group.Id, IsDisabled = false, LifecycleId = lifecycle.Id});

            Log.Info("Project created. ID: " + project.Id);
        }
    }
}