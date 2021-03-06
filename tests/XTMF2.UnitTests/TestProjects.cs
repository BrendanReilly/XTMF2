﻿/*
    Copyright 2017-2019 University of Toronto

    This file is part of XTMF2.

    XTMF2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF2.  If not, see <http://www.gnu.org/licenses/>.
*/
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using TestXTMF;
using TestXTMF.Modules;
using XTMF2.Editing;

namespace XTMF2
{
    [TestClass]
    public class TestProjects
    {
        [TestMethod]
        public void CreateNewProject()
        {
            var runtime = XTMFRuntime.CreateRuntime();
            var controller = runtime.ProjectController;
            string error = null;
            var localUser = TestHelper.GetTestUser(runtime);
            // delete the project in case it has survived.
            controller.DeleteProject(localUser, "Test", ref error);
            Assert.IsTrue(controller.CreateNewProject(localUser, "Test", out ProjectSession session, ref error).UsingIf(session, () =>
            {
                var project = session.Project;
                Assert.AreEqual("Test", project.Name);
                Assert.AreEqual(localUser, project.Owner);
            }), "Unable to create project");
            Assert.IsTrue(controller.DeleteProject(localUser, "Test", ref error));
            Assert.IsFalse(controller.DeleteProject(localUser, "Test", ref error));
        }

        [TestMethod]
        public void RenameProject()
        {
            var runtime = XTMFRuntime.CreateRuntime();
            var controller = runtime.ProjectController;
            string error = null;
            var localUser = TestHelper.GetTestUser(runtime);
            // delete the project in case it has survived.
            controller.DeleteProject(localUser, "Test", ref error);
            Assert.IsTrue(controller.CreateNewProject(localUser, "Test", out ProjectSession session, ref error).UsingIf(session, () =>
            {
                var project = session.Project;
                Assert.AreEqual("Test", project.Name);
                Assert.AreEqual(localUser, project.Owner);
                Assert.IsFalse(controller.RenameProject(localUser, project, "RenamedTestProject", ref error),
                    "RenameProject succeeded even through it was currently being edited!");
            }), "Unable to create project");
            Assert.IsTrue(controller.GetProject(localUser, "Test", out var project, ref error), error);
            Assert.IsTrue(controller.RenameProject(localUser, project, "RenamedTestProject", ref error), error);
            Assert.IsTrue(controller.DeleteProject(localUser, project, ref error), "Failed to cleanup the project.");
        }

        [TestMethod]
        public void ProjectPersistance()
        {
            var runtime = XTMFRuntime.CreateRuntime();
            var controller = runtime.ProjectController;
            string projectName = "Test";
            string error = null;
            var localUser = TestHelper.GetTestUser(runtime);
            // delete the project just in case it survived
            controller.DeleteProject(localUser, projectName, ref error);
            // now create it
            Assert.IsTrue(controller.CreateNewProject(localUser, projectName, out ProjectSession session, ref error).UsingIf(session, () =>
            {
                var project = session.Project;
                Assert.AreEqual(projectName, project.Name);
                Assert.AreEqual(localUser, project.Owner);
            }), "Unable to create project");
            var numberOfProjects = localUser.AvailableProjects.Count;
            // Simulate a shutdown of XTMF
            runtime.Shutdown();
            //Startup XTMF again
            runtime = XTMFRuntime.CreateRuntime();
            controller = runtime.ProjectController;
            localUser = TestHelper.GetTestUser(runtime);
            Assert.AreEqual(numberOfProjects, localUser.AvailableProjects.Count);
            var regainedProject = localUser.AvailableProjects[0];
            Assert.AreEqual(projectName, regainedProject.Name);
        }

        [TestMethod]
        public void EnsureSameProjectSession()
        {
            var runtime = XTMFRuntime.CreateRuntime();
            var controller = runtime.ProjectController;
            string error = null;
            var localUser = TestHelper.GetTestUser(runtime);
            runtime.UserController.Delete("NewUser");
            Assert.IsTrue(runtime.UserController.CreateNew("NewUser", false, out var newUser, ref error), error);
            // delete the project in case it has survived.
            controller.DeleteProject(localUser, "Test", ref error);
            Assert.IsTrue(controller.CreateNewProject(localUser, "Test", out ProjectSession session, ref error).UsingIf(session, () =>
            {
                var project = session.Project;
                Assert.IsTrue(session.ShareWith(localUser, newUser, ref error), error);
                Assert.IsTrue(controller.GetProjectSession(newUser, project, out var session2, ref error).UsingIf(session2, () =>
                {
                    Assert.AreSame(session, session2);
                }), error);
            }), "Unable to create project");

            // cleanup
            Assert.IsTrue(controller.DeleteProject(localUser, "Test", ref error));
        }

        [TestMethod]
        public void EnsureDifferentProjectSession()
        {
            /* When a project session is closed it should be disposed of.
             * A subsequent request for a project session to the same project should
             * be a new object.
             */
            var runtime = XTMFRuntime.CreateRuntime();
            var controller = runtime.ProjectController;
            string error = null;
            var localUser = TestHelper.GetTestUser(runtime);
            runtime.UserController.Delete("NewUser");
            Assert.IsTrue(runtime.UserController.CreateNew("NewUser", false, out var newUser, ref error), error);
            // delete the project in case it has survived.
            controller.DeleteProject(localUser, "Test", ref error);
            Project project = null;
            Assert.IsTrue(controller.CreateNewProject(localUser, "Test", out ProjectSession session, ref error).UsingIf(session, () =>
            {
                project = session.Project;
                Assert.IsTrue(session.ShareWith(localUser, newUser, ref error), error);

            }), "Unable to create project");
            Assert.IsTrue(controller.GetProjectSession(newUser, project, out var session2, ref error).UsingIf(session2, () =>
            {
                Assert.AreNotSame(session, session2);
            }), error);

            // cleanup
            Assert.IsTrue(controller.DeleteProject(localUser, "Test", ref error));
        }

        private static void CreateModelSystem(User user, ProjectSession project, string msName, string description, Action executeBeforeSessionClosed)
        {
            string error = null;
            var startName = "MyStart";
            var nodeName = "MyNode";
            Assert.IsTrue(project.CreateNewModelSystem(user, msName, out var msHeader, ref error), error);
            Assert.IsTrue(msHeader.SetDescription(project, description, ref error), error);
            Assert.IsTrue(project.EditModelSystem(user, msHeader, out var session, ref error), error);
            using (session)
            {
                var ms = session.ModelSystem;
                Assert.IsTrue(session.AddModelSystemStart(user, ms.GlobalBoundary, startName, out var start, ref error), error);
                Assert.IsTrue(session.AddNode(user, ms.GlobalBoundary, nodeName, typeof(IgnoreResult<string>), out var node, ref error), error);
                Assert.IsTrue(session.AddLink(user, start, TestHelper.GetHook(start.Hooks, "ToExecute"), node, out var link, ref error), error);
                Assert.IsTrue(session.Save(ref error), error);
                if (!(executeBeforeSessionClosed is null))
                {
                    executeBeforeSessionClosed();
                }
            }
        }

        [TestMethod]
        public void ExportProjectNoModelSystems()
        {
            TestHelper.RunInProjectContext("ExportProjectNoModelSystems", (user, project) =>
            {
                string error = null;
                var tempFile = new FileInfo(Path.GetTempFileName());
                try
                {
                    // Make sure the file does not exist before starting.
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                    Assert.IsTrue(project.ExportProject(user, tempFile.FullName, ref error), error);
                    Assert.IsTrue(tempFile.Exists, "The exported file does not exist!");
                }
                finally
                {
                    tempFile.Refresh();
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                }
            });
        }

        [TestMethod]
        public void ExportProjectSingleModelSystem()
        {
            TestHelper.RunInProjectContext("ExportProjectSingleModelSystem", (user, project) =>
            {
                string error = null;
                var msName = "MSToExport";
                var description = "A test model system.";
                var tempFile = new FileInfo(Path.GetTempFileName());
                try
                {
                    // Make sure the file does not exist before starting.
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                    CreateModelSystem(user, project, msName, description, () =>
                    {
                        Assert.IsFalse(project.ExportProject(user, tempFile.FullName, ref error), "We were able to export a project while a model system was being edited!");
                    });
                    Assert.IsTrue(project.ExportProject(user, tempFile.FullName, ref error), error);
                    tempFile.Refresh();
                    Assert.IsTrue(tempFile.Exists, "The exported file does not exist!");
                }
                finally
                {
                    tempFile.Refresh();
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                }
            });
        }

        [TestMethod]
        public void ExportProjectMultipleModelSystems()
        {
            TestHelper.RunInProjectContext("ExportProjectMultipleModelSystems", (user, project) =>
            {
                string error = null;
                var msName = "MSToExport";
                var description = "A test model system.";

                var tempFile = new FileInfo(Path.GetTempFileName());
                try
                {
                    // Make sure the file does not exist before starting.
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                    for (int i = 0; i < 5; i++)
                    {
                        CreateModelSystem(user, project, msName + i, description + i, null);
                    }
                    Assert.IsTrue(project.ExportProject(user, tempFile.FullName, ref error), error);
                    Assert.IsTrue(tempFile.Exists, "The exported file does not exist!");
                }
                finally
                {
                    tempFile.Refresh();
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                }
            });
        }

        [TestMethod]
        public void ImportProjectFileNoModelSystem()
        {
            TestHelper.RunInProjectContext("ImportProjectFileNoModelSystem", (XTMFRuntime runtime, User user, ProjectSession project) =>
            {
                string error = null;
                var tempFile = new FileInfo(Path.GetTempFileName());
                try
                {
                    // Make sure the file does not exist before starting.
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                    Assert.IsTrue(project.ExportProject(user, tempFile.FullName, ref error), error);
                    Assert.IsTrue(tempFile.Exists, "The exported file does not exist!");
                    var projectController = runtime.ProjectController;
                    Assert.IsTrue(projectController.ImportProjectFile(user, "ImportProjectFileNoModelSystem-Imported", tempFile.FullName,
                        out var importedSession, ref error), error);
                    Assert.IsNotNull(importedSession);
                    using (importedSession)
                    {
                        var modelSystems = importedSession.ModelSystems;
                        Assert.AreEqual(0, modelSystems.Count);
                    }
                }
                finally
                {
                    tempFile.Refresh();
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                }
            });
        }

        [TestMethod]
        public void ImportProjectFileSingleModelSystem()
        {
            TestHelper.RunInProjectContext("ImportProjectFileSingleModelSystem", (XTMFRuntime runtime, User user, ProjectSession project) =>
            {
                string error = null;
                var tempFile = new FileInfo(Path.GetTempFileName());
                try
                {
                    // Make sure the file does not exist before starting.
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                    CreateModelSystem(user, project, "ModelSystem1", "A single model system", null);
                    Assert.IsTrue(project.ExportProject(user, tempFile.FullName, ref error), error);
                    Assert.IsTrue(tempFile.Exists, "The exported file does not exist!");
                    var projectController = runtime.ProjectController;
                    Assert.IsTrue(projectController.ImportProjectFile(user, "ImportProjectFileSingleModelSystem-Imported", tempFile.FullName,
                        out var importedSession, ref error), error);
                    Assert.IsNotNull(importedSession);
                    using (importedSession)
                    {
                        var modelSystems = importedSession.ModelSystems;
                        Assert.AreEqual(1, modelSystems.Count);
                    }
                }
                finally
                {
                    tempFile.Refresh();
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                }
            });
        }

        [TestMethod]
        public void ImportProjectFileMultipleModelSystem()
        {
            TestHelper.RunInProjectContext("ImportProjectFileSingleModelSystem", (XTMFRuntime runtime, User user, ProjectSession project) =>
            {
                string error = null;
                var tempFile = new FileInfo(Path.GetTempFileName());
                try
                {
                    // Make sure the file does not exist before starting.
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                    const int numberOfModelSystems = 5;
                    for (int i = 0; i < numberOfModelSystems; i++)
                    {
                        CreateModelSystem(user, project, "ModelSystem" + i, "One of many model systems", null);
                    }
                    Assert.IsTrue(project.ExportProject(user, tempFile.FullName, ref error), error);
                    Assert.IsTrue(tempFile.Exists, "The exported file does not exist!");
                    var projectController = runtime.ProjectController;
                    Assert.IsTrue(projectController.ImportProjectFile(user, "ImportProjectFileSingleModelSystem-Imported", tempFile.FullName,
                        out var importedSession, ref error), error);
                    Assert.IsNotNull(importedSession);
                    using (importedSession)
                    {
                        var modelSystems = importedSession.ModelSystems;
                        Assert.AreEqual(numberOfModelSystems, modelSystems.Count);
                    }
                }
                finally
                {
                    tempFile.Refresh();
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                }
            });
        }

        [TestMethod]
        public void ImportProjectFileAddedToController()
        {
            const string ImportedModelSystemName = "ImportProjectFileNoModelSystemPersist-Imported";
            TestHelper.RunInProjectContext("ImportProjectFileNoModelSystemPersist", (XTMFRuntime runtime, User user, ProjectSession project) =>
            {
                string error = null;
                var tempFile = new FileInfo(Path.GetTempFileName());
                try
                {
                    // Make sure the file does not exist before starting.
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                    Assert.IsTrue(project.ExportProject(user, tempFile.FullName, ref error), error);
                    Assert.IsTrue(tempFile.Exists, "The exported file does not exist!");
                    var projectController = runtime.ProjectController;

                    Assert.IsTrue(projectController.ImportProjectFile(user, ImportedModelSystemName, tempFile.FullName,
                        out var importedSession, ref error), error);
                    Assert.IsNotNull(importedSession);
                    Assert.IsTrue(user.AvailableProjects.Any(p => p.Name == ImportedModelSystemName), "The imported project was not available to use user.");
                    using (importedSession)
                    {
                        var modelSystems = importedSession.ModelSystems;
                        Assert.AreEqual(0, modelSystems.Count);
                    }
                    Assert.IsTrue(user.AvailableProjects.Any(p => p.Name == ImportedModelSystemName), "The imported project was not available to use user.");

                }
                finally
                {
                    tempFile.Refresh();
                    if (tempFile.Exists)
                    {
                        tempFile.Delete();
                    }
                }
            });
        }

        [TestMethod]
        public void SetRunsDirectory()
        {
            TestHelper.RunInProjectContext("SetRunsDirectory", (User user, ProjectSession project) =>
            {
                var dir = new DirectoryInfo(Guid.NewGuid().ToString());
                try
                {
                    string error = null;
                    dir.Create();
                    Assert.IsTrue(project.SetCustomRunDirectory(user, dir.FullName, ref error), error);
                    Assert.AreEqual(dir.FullName, project.RunsDirectory);
                }
                finally
                {
                    if (dir.Exists)
                    {
                        dir.Delete();
                    }
                }
            });
        }

        [TestMethod]
        public void SetRunsDirectoryToInvalidPath()
        {
            TestHelper.RunInProjectContext("SetRunsDirectoryToInvalidPath", (User user, ProjectSession project) =>
            {
                string error = null;
                var initialDirectory = project.RunsDirectory;
                const string anInvalidPath = ":?!@#://#%$^&|";
                Assert.IsFalse(project.SetCustomRunDirectory(user, anInvalidPath, ref error), "An invalid path was able to be set.");
                Assert.AreEqual(initialDirectory, project.RunsDirectory);
            });
        }

        [TestMethod]
        public void ResetRunsDirectory()
        {
            TestHelper.RunInProjectContext("ResetRunsDirectory", (User user, ProjectSession project) =>
            {
                var newRunDir = Guid.NewGuid().ToString();
                DirectoryInfo dir = new DirectoryInfo(newRunDir);
                try
                {
                    string error = null;
                    dir.Create();
                    var initialDirectory = project.RunsDirectory;
                    Assert.IsTrue(project.SetCustomRunDirectory(user, dir.FullName, ref error), error);
                    Assert.AreEqual(dir.FullName, project.RunsDirectory);
                    Assert.IsTrue(project.ResetCustomRunDirectory(user, ref error), error);
                    Assert.AreEqual(initialDirectory, project.RunsDirectory);
                }
                finally
                {
                    if (dir.Exists)
                    {
                        dir.Delete();
                    }
                }
            });
        }

        [TestMethod]
        public void RemoveModelSystem()
        {
            TestHelper.RunInProjectContext("RemoveModelSystem", (User user, ProjectSession project) =>
            {
                const string msName = "RemoveMe";
                string error = null;
                Assert.IsTrue(project.CreateNewModelSystem(user, msName, out var msHeader, ref error), error);
                Assert.IsTrue(project.EditModelSystem(user, msHeader, out var session, ref error).UsingIf(session, ()=>
                {
                    Assert.IsFalse(project.RemoveModelSystem(user, msHeader, ref error), "A model system was able to be removed while it was being edited!");
                }), error);
                Assert.IsTrue(project.RemoveModelSystem(user, msHeader, ref error), error);
            });
        }
    }
}
