using StaffValidator.Core.Services;
using Xunit;

namespace StaffValidator.Tests
{
    public class AutomataTests
    {
        [Theory]
        [InlineData("alice@example.com", true)]
        [InlineData("carol@example.co.uk", true)]
        [InlineData("not-an-email", false)]
        public void EmailNfa_RecognizesExpected(string input, bool expected)
        {
            var nfa = AutomataFactory.BuildEmailNfa();
            var ok = nfa.Simulate(input);
            Assert.Equal(expected, ok);
        }

        [Theory]
        [InlineData("+44 1234 567890", true)]
        [InlineData("123-456-7890", true)]
        [InlineData("abc123", false)]
        public void PhoneNfa_RecognizesExpected(string input, bool expected)
        {
            var nfa = AutomataFactory.BuildPhoneNfa();
            var ok = nfa.Simulate(input);
            Assert.Equal(expected, ok);
        }
    }
}
