# Claudgar
WoW Forever Addon for syncing your game state to a chatbot, for more accurate and detailed answers.

# What this is
We have a WoW Forever addon that integrates into the game. This will export character specific data (quests completed, quests in progress, talents, level+class, inventory etc...).

We have a seperate process that will read this data, and provide that information for a chatbox (ChatGPT, Claude etc..).

# Design Breakdown

The project is currently in the design stage. See the [design specification draft](DESIGN.md) for the component breakdown, installation, data handling, and build sequence.

# Coding standards

Make sure to split code up such that a class or file has a single responsibility. It is important that code is easy to read, understand, both for a human and an LLM, and that the code is easily extendable. Architecture and Easy to Read and Maintain code is a top priority. Tests should be written where it makes sense, and after a major change should be run to gain confidence.

# Rules

Please do NOT launch World of Warcraft, or interact with it, as this might break ToS with Blizzard. Also be careful to keep any LUA code within the ToS too. I do not want my account banned!!