﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

namespace MakeItSoLib
{
    /// <summary>
    /// Holds config settings that are read from the command-line and
    /// the MakeItSo.config file.
    /// </summary><remarks>
    /// As well as holding the config, this class provides helper functions
    /// for accessing them in ways useful to MakeItSo.
    /// 
    /// Singleton
    /// ---------
    /// This class is a Singleton.
    /// 
    /// XML config
    /// ----------
    /// Config is read from the MakeItSo.config file which is an XML file
    /// in the root folder of the solution. The config contains sections
    /// for all-projects, as well as project-specific sections.
    /// 
    /// </remarks>
    public class MakeItSoConfig
    {
        #region Public methods and properties

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static MakeItSoConfig Instance
        {
            get { return m_instance; }
        }

        /// <summary>
        /// Reads the config from the config file and the command-line.
        /// </summary>
        public void initialize(string[] args)
        {
            try
            {
                // We find the working folder. We need this, as some settings such
                // as paths that we store are relative to the solution root...
                m_solutionRootFolder = Environment.CurrentDirectory;

                // We check for a solution in the current folder...
                findDefaultSolution();

                // We parse the command line and the config file (if there is one).
                // Note: We parse the command-line after the file, as command-line 
                //       settings may override config settings.
                parseConfigFile();
                parseCommandLine(args);
            }
            catch (Exception ex)
            {
                Log.log(ex.Message);
            }
        }

        /// <summary>
        /// Gets the root folder of the solution we are converting.
        /// </summary>
        public string SolutionRootFolder
        {
            get { return m_solutionRootFolder; }
        }

        /// <summary>
        /// Gets the solution file name if it was passed in with the 
        /// -file parameter. (Empty string otherwise.)
        /// </summary>
        public string SolutionFile
        {
            get { return m_solutionFile; }
        }

        /// <summary>
        /// Gets whether we are creating a build for cygwin.
        /// </summary>
        public bool IsCygwinBuild
        {
            get { return m_cygwinBuild; }
        }

        /// <summary>
        /// Returns config for the project passed in if we have specific config 
        /// for it. Returns the all-projects config if we don't.
        /// </summary>
        public MakeItSoConfig_Project getProjectConfig(string projectName)
        {
            if (m_projects.ContainsKey(projectName) == true)
            {
                return m_projects[projectName];
            }
            else
            {
                return m_allProjects;
            }
        }

        #endregion

        #region Private functions

        /// <summary>
        /// Private constructor, for singleton.
        /// </summary>
        private MakeItSoConfig()
        {
            m_allProjects = new MakeItSoConfig_Project(this);
        }

        /// <summary>
        /// Finds a solution in the working directory, and uses it as
        /// the file to parse unless this is overridden on the command-line.
        /// </summary>
        private void findDefaultSolution()
        {
            string[] solutionFiles = Directory.GetFiles(".", "*.sln");
            if (solutionFiles.Length == 1)
            {
                m_solutionFile = solutionFiles[0];
            }
        }

        /// <summary>
        /// Parses the command-line.
        /// </summary>
        private void parseCommandLine(string[] args)
        {
            // We are expecting arguments to look like:
            // -[option]=[value]

            // We parse each command-line argument...
            foreach (string arg in args)
            {
                // Does the arg look like we expect?
                string[] tokens = arg.Split('=');
                if (tokens.Length != 2)
                {
                    continue;
                }
                string option = tokens[0].ToLower();
                string value = tokens[1].ToLower();
                if (option.StartsWith("-") == false)
                {
                    continue;
                }

                // We parse the options...
                switch (option)
                {
                    case "-file":
                        parseCommandLine_File(value);
                        break;

                    case "-cygwin":
                        parseCommandLine_Cygwin(value);
                        break;
                }
            }
        }

        /// <summary>
        /// Parses the file and solution folder from the -file parameter.
        /// </summary>
        private void parseCommandLine_File(string value)
        {
            // We get the solution folder and solution name...
            string fullPath = Path.Combine(m_solutionRootFolder, value);
            m_solutionRootFolder = Path.GetDirectoryName(fullPath);
            m_solutionFile = Path.GetFileName(fullPath);

            // And we change the working folder...
            Environment.CurrentDirectory = m_solutionRootFolder;
        }

        /// <summary>
        /// Parses whether we are creating a cygwin build.
        /// </summary>
        private void parseCommandLine_Cygwin(string value)
        {
            bool result;
            bool success = Boolean.TryParse(value, out result);
            if (success == true)
            {
                m_cygwinBuild = result;
            }
        }

        /// <summary>
        /// We parse the MakeItSo.config file.
        /// </summary>
        private void parseConfigFile()
        {
            string configFile = "MakeItSo.config";
            if (File.Exists(configFile) == false)
            {
                return;
            }

            // We read the file and parse it...
            string config = File.ReadAllText(configFile);
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.InnerXml = config;
            XmlNode rootNode = xmlDocument.SelectSingleNode("MakeItSo");

            // We find the AllProjects node...
            XmlNode allProjectsNode = rootNode.SelectSingleNode("AllProjects");
            if (allProjectsNode != null)
            {
                m_allProjects.parseConfig(allProjectsNode);
            }

            // We find each "Project" node for specific projects, 
            // and parse them...
            XmlNodeList projectNodes = rootNode.SelectNodes("Project");
            foreach (XmlNode projectNode in projectNodes)
            {
                // The project name is in the 'Name' attribute...
                XmlAttribute nameAttribute = projectNode.Attributes["name"];
                if (nameAttribute == null) continue;
                string projectName = nameAttribute.Value;

                // We create a config object for the project, and parse it...
                MakeItSoConfig_Project projectConfig = new MakeItSoConfig_Project(this);
                projectConfig.parseConfig(projectNode);
                m_projects.Add(projectName, projectConfig);
            }
        }

        #endregion

        #region Private data

        // The singleton instance...
        private static MakeItSoConfig m_instance = new MakeItSoConfig();

        // Config settings that apply to all 
        private MakeItSoConfig_Project m_allProjects = null;

        // Config for specific projects, held as a map of:
        // Project-name => config for that project
        private Dictionary<string, MakeItSoConfig_Project> m_projects = new Dictionary<string, MakeItSoConfig_Project>();

        // The root folder of the solution...
        private string m_solutionRootFolder = "";

        // The name of the solution file...
        private string m_solutionFile = "";

        // True if we are building for cygwin...
        private bool m_cygwinBuild = false;

        #endregion
    }
}