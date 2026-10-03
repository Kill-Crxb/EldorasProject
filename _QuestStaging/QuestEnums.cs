public enum QuestType { Main, Side, Commission, Hidden }

/// <summary>
/// What an objective asks for. Event kinds count GameplayEvents; Have and Flag are state
/// checks that are re-read whenever inventory or flags change, so they can un-complete.
/// </summary>
public enum ObjectiveType { Kill, Find, Reach, Interact, Submit, Have, Flag }

public enum SubmitMode { HandIn, Show }

public enum FlagCompare { Equal, NotEqual, AtLeast, AtMost }
