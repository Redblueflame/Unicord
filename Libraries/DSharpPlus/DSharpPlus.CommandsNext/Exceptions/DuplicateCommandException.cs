﻿using System;

namespace DSharpPlus.CommandsNext.Exceptions
{
    /// <summary>
    /// Indicates that given command name or alias is taken.
    /// </summary>
    public class DuplicateCommandException : Exception
    {
        /// <summary>
        /// Gets the name of the command that already exists.
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// Creates a new exception indicating that given command name is already taken.
        /// </summary>
        /// <param name="name">Name of the command that was taken.</param>
        internal DuplicateCommandException(string name)
            : base("A command with specified name already exists.")
        {
            CommandName = name;
        }

        /// <summary>
        /// Returns a string representation of this <see cref="DuplicateCommandException"/>.
        /// </summary>
        /// <returns>A string representation.</returns>
        public override string ToString()
        {
            return $"{GetType()}: {Message}\nCommand name: {CommandName}"; // much like System.ArgumentException works
        }
    }
}
