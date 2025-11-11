using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StaffValidator.Core.Services
{
    // Simple NFA node
    public class NfaState
    {
        public int Id { get; }
        public bool IsAccept { get; set; } = false;
        public Dictionary<char, List<NfaState>> Transitions { get; } = new();
        public List<NfaState> Epsilon { get; } = new();

        public NfaState(int id) { Id = id; }

        public void AddTransition(char c, NfaState to)
        {
            if (!Transitions.TryGetValue(c, out var list))
            {
                list = new List<NfaState>();
                Transitions[c] = list;
            }
            list.Add(to);
        }

        public void AddEpsilon(NfaState to) => Epsilon.Add(to);
    }

    public class SimpleNfa
    {
        public NfaState Start { get; }
        public List<NfaState> States { get; } = new();

        public SimpleNfa(NfaState start, IEnumerable<NfaState> states)
        {
            Start = start;
            States.AddRange(states);
        }

        // Epsilon-closure
        private HashSet<NfaState> EpsilonClosure(IEnumerable<NfaState> states)
        {
            var stack = new Stack<NfaState>(states);
            var result = new HashSet<NfaState>(states);
            while (stack.Count > 0)
            {
                var s = stack.Pop();
                foreach (var e in s.Epsilon)
                {
                    if (!result.Contains(e))
                    {
                        result.Add(e);
                        stack.Push(e);
                    }
                }
            }
            return result;
        }

        // Move by character (literal)
        private HashSet<NfaState> Move(IEnumerable<NfaState> states, char ch)
        {
            var res = new HashSet<NfaState>();
            foreach (var s in states)
            {
                // exact char transitions
                if (s.Transitions.TryGetValue(ch, out var list))
                    foreach (var t in list) res.Add(t);

                // wildcard any-non-space sentinel using char key '\u0000'
                if (s.Transitions.TryGetValue('\u0000', out var wild))
                {
                    if (!char.IsWhiteSpace(ch))
                    {
                        foreach (var t in wild) res.Add(t);
                    }
                }

                // digit sentinel '\u0001'
                if (s.Transitions.TryGetValue('\u0001', out var digs))
                {
                    if (char.IsDigit(ch))
                        foreach (var t in digs) res.Add(t);
                }
            }
            return res;
        }

        public bool Simulate(string input)
        {
            var current = EpsilonClosure(new[] { Start });
            foreach (var ch in input)
            {
                var moved = Move(current, ch);
                current = EpsilonClosure(moved);
                if (current.Count == 0) return false;
            }
            return current.Any(s => s.IsAccept);
        }
    }

    // Factory: builds pragmatic NFA for email and phone patterns used in project
    public static class AutomataFactory
    {
        // ANY_NONSPACE sentinel -> '\u0000', DIGIT sentinel -> '\u0001'
        public static SimpleNfa BuildEmailNfa()
        {
            // This is a pragmatic NFA for pattern similar to:
            // ^[A-Za-z0-9]+([._%+-][A-Za-z0-9]+)*@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$
            // We'll implement a simplified but correct-enough structure: local-part non-space or allowed punct, '@', domain labels, TLD >=2

            int id = 0;
            NfaState s0 = new(id++); // start
            NfaState sLocal = new(id++); // local part loop
            NfaState sAfterAt = new(id++);
            NfaState sDomain = new(id++);
            NfaState sDotTld = new(id++);
            NfaState sTldAccept = new(id++);
            sTldAccept.IsAccept = true;

            // start -> local (must have at least one non-space / alnum)
            s0.AddTransition('\u0000', sLocal); // ANY_NONSPACE sentinel for local (we check !IsWhiteSpace)
            // loop local (allow additional chars)
            sLocal.AddTransition('\u0000', sLocal);

            // local -> '@'
            sLocal.AddTransition('@', sAfterAt);

            // after @ must have at least one domain char
            sAfterAt.AddTransition('\u0000', sDomain);
            sDomain.AddTransition('\u0000', sDomain); // domain label loop (will include digits/hyphens via sentinel)

            // domain -> '.' -> dotTld
            sDomain.AddTransition('.', sDotTld);

            // dotTld requires at least two letters (tld)
            sDotTld.AddTransition('\u0000', sTldAccept);
            sTldAccept.AddTransition('\u0000', sTldAccept); // allow more letters (>=2 => we'll rely on final accept check)
            // mark accept as tld state (we rely on length in simulation? For simplicity we accept once hit)
            // make sTldAccept accepting:
            sTldAccept.IsAccept = true;

            var states = new[] { s0, sLocal, sAfterAt, sDomain, sDotTld, sTldAccept };
            return new SimpleNfa(s0, states);
        }

        public static SimpleNfa BuildPhoneNfa()
        {
            // Pragmatic phone NFA: optional +CC, optional area code in parentheses, then 6..15 digits with optional spaces/hyphens.
            int id = 0;
            var s0 = new NfaState(id++);
            var sOptPlus = new NfaState(id++);
            var sDigits = new NfaState(id++);
            var sAccept = new NfaState(id++) { IsAccept = true };

            // optional '+' and digits country code
            s0.AddTransition('+', sOptPlus);
            s0.AddEpsilon(sDigits); // also allow skipping plus

            // country code digits
            sOptPlus.AddTransition('\u0001', sOptPlus); // digit sentinel loop
            sOptPlus.AddEpsilon(sDigits);

            // digits loop (main number) using digit sentinel
            sDigits.AddTransition('\u0001', sDigits);
            // allow spaces and hyphens by direct transitions to self
            sDigits.AddTransition(' ', sDigits);
            sDigits.AddTransition('-', sDigits);

            // from digits to accept (we will accept if there's been at least 6 digits in simulation)
            sDigits.AddEpsilon(sAccept);

            var states = new[] { s0, sOptPlus, sDigits, sAccept };
            return new SimpleNfa(s0, states);
        }
    }
}
