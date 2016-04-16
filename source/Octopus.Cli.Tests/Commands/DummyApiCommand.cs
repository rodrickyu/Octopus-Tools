﻿using System;
using log4net;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Util;

namespace Octopus.Cli.Tests.Commands
{
    public class DummyApiCommand : ApiCommand
    {
        string pill;
        public DummyApiCommand(IOctopusRepositoryFactory repositoryFactory, ILog log, IOctopusFileSystem fileSystem)
            : base(repositoryFactory, log, fileSystem)
        {
            var options = Options.For("Dummy");
            options.Add("pill=", "Red or Blue. Blue, the story ends. Red, stay in Wonderland and see how deep the rabbit hole goes.", v => pill = v);
			log.Debug ("Pill: " + pill);
        }

        protected override void Execute()
        {
            Assert.Pass();
        }
    }
}
