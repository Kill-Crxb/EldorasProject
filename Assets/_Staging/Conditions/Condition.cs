using System;
using System.Collections.Generic;
using UnityEngine;

// One way to ask "is this true right now" (Audit_Conditions_State.md P1). A Fact is the capability question
// ("target CannotDodge", "self WieldingHeavy"); a Status is the flavour question ("target has Sundered"); Behind is
// geometry between the two fighters. A list holds when every entry holds, so "one of" is written as a broader fact
// or with Not. A Target condition with no target fails.
namespace CrabSystem
{
    public enum ConditionKind { Fact, Status, Behind }

    public enum ConditionSubject { Self, Target }

    [Serializable]
    public struct Condition
    {
        public ConditionKind kind;
        public ConditionSubject subject;
        [Tooltip("Fact: the blackboard fact that must be true.")]
        [IdRef(IdKind.Fact)] public string fact;
        [Tooltip("Status: the status that must be on the subject.")]
        public StatusDefinition status;
        [Tooltip("Behind: self counts as behind outside this many degrees of the target's facing.")]
        [Range(90f, 180f)] public float behindAngle;
        [Tooltip("Holds when the check is false instead.")]
        public bool not;
    }

    public static class Conditions
    {
        public static bool Hold(List<Condition> conditions, ControllerBrain self, ControllerBrain target)
        {
            if (conditions == null) return true;

            foreach (Condition condition in conditions)
            {
                if (Check(condition, self, target) == condition.not) return false;
            }
            return true;
        }

        public static bool ReadsStatus(List<Condition> conditions)
        {
            if (conditions == null) return false;

            foreach (Condition condition in conditions)
            {
                if (condition.kind == ConditionKind.Status) return true;
            }
            return false;
        }

        public static void AddFactKeys(List<Condition> conditions, HashSet<int> keys)
        {
            if (conditions == null) return;

            foreach (Condition condition in conditions)
            {
                if (condition.kind == ConditionKind.Fact && !string.IsNullOrEmpty(condition.fact))
                    keys.Add(BlackboardKey.Hash(condition.fact));
            }
        }

        public static string Describe(List<Condition> conditions)
        {
            if (conditions == null || conditions.Count == 0) return "always";

            var parts = new List<string>(conditions.Count);
            foreach (Condition condition in conditions) parts.Add(Describe(condition));
            return string.Join(", ", parts);
        }

        private static bool Check(Condition condition, ControllerBrain self, ControllerBrain target)
        {
            ControllerBrain subject = condition.subject == ConditionSubject.Self ? self : target;
            if (subject == null) return false;

            if (condition.kind == ConditionKind.Fact) return HasFact(subject, condition.fact);
            if (condition.kind == ConditionKind.Status) return HasStatus(subject, condition.status);
            return IsBehind(self, target, condition.behindAngle);
        }

        private static bool HasFact(ControllerBrain who, string fact)
        {
            Blackboard blackboard = who.Blackboard;
            return blackboard != null && !string.IsNullOrEmpty(fact) && blackboard.GetBool(BlackboardKey.Hash(fact));
        }

        private static bool HasStatus(ControllerBrain who, StatusDefinition status)
        {
            StatusSystem statuses = status != null ? who.GetModule<StatusSystem>() : null;
            return statuses != null && statuses.Has(status.id);
        }

        private static bool IsBehind(ControllerBrain self, ControllerBrain target, float angle)
        {
            if (self == null || target == null) return false;

            Transform them = target.EntityRoot != null ? target.EntityRoot : target.transform;
            Transform me = self.EntityRoot != null ? self.EntityRoot : self.transform;
            Vector3 toMe = me.position - them.position;
            toMe.y = 0f;
            if (toMe.sqrMagnitude < 0.0001f) return false;

            return Vector3.Angle(them.forward, toMe) >= angle;
        }

        private static string Describe(Condition condition)
        {
            string who = condition.subject == ConditionSubject.Self ? "" : "target ";
            string not = condition.not ? "not " : "";
            if (condition.kind == ConditionKind.Fact) return $"{who}{not}{condition.fact}";
            if (condition.kind == ConditionKind.Status) return $"{who}{not}{(condition.status != null ? condition.status.displayName : "?")}";
            return $"{not}from behind";
        }
    }
}
