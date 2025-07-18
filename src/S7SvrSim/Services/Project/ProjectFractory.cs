﻿using S7SvrSim.S7Signal;
using System;
using System.IO;

namespace S7SvrSim.Services.Project
{
    public class ProjectFractory : IProjectFactory
    {
        private readonly IServiceProvider serviceProvider;
        private readonly SignalsHelper signalsHelper;

        public ProjectFractory(IServiceProvider serviceProvider, SignalsHelper signalsHelper)
        {
            this.serviceProvider = serviceProvider;
            this.signalsHelper = signalsHelper;
        }

        private IProject GetProjectByPath(string path)
        {
            return path == null ? new DefaultProject(serviceProvider, signalsHelper) : new SoftwareProject(path, serviceProvider, signalsHelper);
        }

        public IProject CreateProject(string path)
        {
            var project = GetProjectByPath(path);

            project.New();
            project.Save();

            return project;
        }

        public IProject GetOrCreateProject(string path)
        {
            var project = GetProjectByPath(path);
            path = project.Path;

            if (File.Exists(path))
            {
                project.Load();
            }
            else
            {
                project.New();
                project.Save();
            }

            return project;
        }

        public IProject GetProject(string path)
        {
            var project = GetProjectByPath(path);
            project.Load();
            return project;
        }
    }
}
