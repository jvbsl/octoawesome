#region Using Statements
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#endregion

namespace OctoAwesome.Client
{
#if WINDOWS || LINUX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        static OctoGame game;
        private static Logger logger;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var config = new LoggingConfiguration();

            config.AddRule(LogLevel.Error, LogLevel.Fatal, new ColoredConsoleTarget("octoawesome.client.logconsole"));
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, new FileTarget("octoawesome.client.logfile") { FileName = Path.Combine("logs", "client.log") });

            LogManager.Configuration = config;
            logger = LogManager.GetCurrentClassLogger();

            logger.Info("Start game");

            using (game = new OctoGame())
                game.Run(60,60);
        }

        public static void Restart()
        {
            logger.Info("Restart game");

            game.Exit();
            using (game = new OctoGame())
                game.Run(60,60);
        }
    }
#endif
}
