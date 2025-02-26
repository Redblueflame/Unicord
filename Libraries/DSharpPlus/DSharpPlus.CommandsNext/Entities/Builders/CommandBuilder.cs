﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.CommandsNext.Exceptions;

namespace DSharpPlus.CommandsNext.Builders
{
    /// <summary>
    /// Represents an interface to build a command.
    /// </summary>
    public class CommandBuilder
    {
        /// <summary>
        /// Gets the name set for this command.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the aliases set for this command.
        /// </summary>
        public IReadOnlyList<string> Aliases { get; }
        private List<string> AliasList { get; }

        /// <summary>
        /// Gets the description set for this command.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Gets whether this command will be hidden or not.
        /// </summary>
        public bool IsHidden { get; private set; }

        /// <summary>
        /// Gets the execution checks defined for this command.
        /// </summary>
        public IReadOnlyList<CheckBaseAttribute> ExecutionChecks { get; }
        private List<CheckBaseAttribute> ExecutionCheckList { get; }

        /// <summary>
        /// Gets the collection of this command's overloads.
        /// </summary>
        public IReadOnlyList<CommandOverloadBuilder> Overloads { get; }
        private List<CommandOverloadBuilder> OverloadList { get; }
        private HashSet<string> OverloadArgumentSets { get; }

        /// <summary>
        /// Gets the module on which this command is to be defined.
        /// </summary>
        public ICommandModule Module { get; }

        /// <summary>
        /// Gets custom attributes defined on this command.
        /// </summary>
        public IReadOnlyList<Attribute> CustomAttributes { get; }
        private List<Attribute> CustomAttributeList { get; }

        /// <summary>
        /// Creates a new module-less command builder.
        /// </summary>
        public CommandBuilder()
            : this(null)
        { }

        /// <summary>
        /// Creates a new command builder.
        /// </summary>
        /// <param name="module">Module on which this command is to be defined.</param>
        public CommandBuilder(ICommandModule module)
        {
            AliasList = new List<string>();
            Aliases = new ReadOnlyCollection<string>(AliasList);

            ExecutionCheckList = new List<CheckBaseAttribute>();
            ExecutionChecks = new ReadOnlyCollection<CheckBaseAttribute>(ExecutionCheckList);

            OverloadArgumentSets = new HashSet<string>();
            OverloadList = new List<CommandOverloadBuilder>();
            Overloads = new ReadOnlyCollection<CommandOverloadBuilder>(OverloadList);

            Module = module;

            CustomAttributeList = new List<Attribute>();
            CustomAttributes = new ReadOnlyCollection<Attribute>(CustomAttributeList);
        }

        /// <summary>
        /// Sets the name for this command.
        /// </summary>
        /// <param name="name">Name for this command.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithName(string name)
        {
            if (name == null || name.ToCharArray().Any(xc => char.IsWhiteSpace(xc)))
                throw new ArgumentException("Command name cannot be null or contain any whitespace characters.", nameof(name));

            if (Name != null)
                throw new InvalidOperationException("This command already has a name.");

            if (AliasList.Contains(name))
                throw new ArgumentException("Command name cannot be one of its aliases.", nameof(name));

            Name = name;
            return this;
        }

        /// <summary>
        /// Adds aliases to this command.
        /// </summary>
        /// <param name="aliases">Aliases to add to the command.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithAliases(params string[] aliases)
        {
            if (aliases == null || !aliases.Any())
                throw new ArgumentException("You need to pass at least one alias.", nameof(aliases));

            foreach (var alias in aliases)
                WithAlias(alias);

            return this;
        }

        /// <summary>
        /// Adds an alias to this command.
        /// </summary>
        /// <param name="alias">Alias to add to the command.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithAlias(string alias)
        {
            if (alias.ToCharArray().Any(xc => char.IsWhiteSpace(xc)))
                throw new ArgumentException("Aliases cannot contain whitespace characters or null strings.", nameof(alias));

            if (Name == alias || AliasList.Contains(alias))
                throw new ArgumentException("Aliases cannot contain the command name, and cannot be duplicate.", nameof(alias));

            AliasList.Add(alias);
            return this;
        }

        /// <summary>
        /// Sets the description for this command.
        /// </summary>
        /// <param name="description">Description to use for this command.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithDescription(string description)
        {
            Description = description;
            return this;
        }

        /// <summary>
        /// Sets whether this command is to be hidden.
        /// </summary>
        /// <param name="hidden">Whether the command is to be hidden.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithHiddenStatus(bool hidden)
        {
            IsHidden = hidden;
            return this;
        }

        /// <summary>
        /// Adds pre-execution checks to this command.
        /// </summary>
        /// <param name="checks">Pre-execution checks to add to this command.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithExecutionChecks(params CheckBaseAttribute[] checks)
        {
            ExecutionCheckList.AddRange(checks.Except(ExecutionCheckList));
            return this;
        }

        /// <summary>
        /// Adds a pre-execution check to this command.
        /// </summary>
        /// <param name="check">Pre-execution check to add to this command.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithExecutionCheck(CheckBaseAttribute check)
        {
            if (!ExecutionCheckList.Contains(check))
                ExecutionCheckList.Add(check);
            return this;
        }

        /// <summary>
        /// Adds overloads to this command. An executable command needs to have at least one overload.
        /// </summary>
        /// <param name="overloads">Overloads to add to this command.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithOverloads(params CommandOverloadBuilder[] overloads)
        {
            foreach (var overload in overloads)
                WithOverload(overload);

            return this;
        }

        /// <summary>
        /// Adds an overload to this command. An executable command needs to have at least one overload.
        /// </summary>
        /// <param name="overload">Overload to add to this command.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithOverload(CommandOverloadBuilder overload)
        {
            if (OverloadArgumentSets.Contains(overload.ArgumentSet))
                throw new DuplicateOverloadException(Name, overload.Arguments.Select(x => x.Type).ToList(), overload.ArgumentSet);

            OverloadArgumentSets.Add(overload.ArgumentSet);
            OverloadList.Add(overload);

            return this;
        }

        /// <summary>
        /// Adds a custom attribute to this command. This can be used to indicate various custom information about a command.
        /// </summary>
        /// <param name="attribute">Attribute to add.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithCustomAttribute(Attribute attribute)
        {
            CustomAttributeList.Add(attribute);
            return this;
        }

        /// <summary>
        /// Adds multiple custom attributes to this command. This can be used to indicate various custom information about a command.
        /// </summary>
        /// <param name="attributes">Attributes to add.</param>
        /// <returns>This builder.</returns>
        public CommandBuilder WithCustomAttributes(params Attribute[] attributes)
        {
            foreach (var attr in attributes)
                WithCustomAttribute(attr);

            return this;
        }

        internal virtual Command Build(CommandGroup parent)
        {
            var cmd = new Command
            {
                Name = Name,
                Description = Description,
                Aliases = Aliases,
                ExecutionChecks = ExecutionChecks,
                IsHidden = IsHidden,
                Parent = parent,
                Overloads = new ReadOnlyCollection<CommandOverload>(Overloads.Select(xo => xo.Build()).ToList()),
                Module = Module,
                CustomAttributes = CustomAttributes
            };

            return cmd;
        }
    }
}
