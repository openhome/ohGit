using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Git
{
    internal class Person : IPerson
    {
        private readonly string iName;
        private readonly string iEmail;

        internal static IPerson Create(string aName, string aEmail)
        {
            return (new Person(aName, aEmail));
        }

        internal Person(string aName, string aEmail)
        {
            iName = aName;
            iEmail = aEmail;
        }

        public string Name
        {
            get
            {
                return (iName);
            }
        }

        public string Email
        {
            get
            {
                return (iEmail);
            }
        }
    }

    internal class PersonTime : IPersonTime
    {
        static DateTime kYearDot = new DateTime(1970, 1, 1);

        private readonly IPerson iPerson;
        private readonly DateTime iTime;
        private readonly string iTimeZone;

        private PersonTime(string aName, string aEmail, DateTime aTime, string aTimeZone)
        {
            iPerson = new Person(aName, aEmail);
            iTime = aTime;
            iTimeZone = aTimeZone;
        }

        public IPerson Person
        {
            get
            {
                return (iPerson);
            }
        }

        public DateTime Time
        {
            get
            {
                return (iTime);
            }
        }

        public string TimeZone
        {
            get
            {
                return (iTimeZone);
            }
        }

        internal static IPersonTime Create(string aValue)
        {
            int seconds;

            int endName = aValue.IndexOf('<');
            int endEmail = aValue.IndexOf('>');

            if (endName < 0 || endEmail < 0)
            {
                return (null);
            }

            string name = aValue.Substring(0, endName).Trim();
            string email = aValue.Substring(endName + 1, endEmail - endName - 1).Trim();
            string time = aValue.Substring(endEmail + 1).Trim();
            string[] parts = time.Split(new char[] { ' ' });

            if (parts.Length != 2)
            {
                return (null);
            }

            if (!int.TryParse(parts[0], out seconds))
            {
                return (null);
            }

            DateTime at = kYearDot.Add(new TimeSpan(0, 0, seconds));

            return (new PersonTime(name, email, at, parts[1]));
        }

        internal static string String(IPerson aPerson)
        {
            TimeSpan span = DateTime.Now.Subtract(kYearDot);
            int seconds = (int)span.TotalSeconds;

            StringBuilder builder = new StringBuilder();

            builder.Append(aPerson.Name);
            builder.Append(" <");
            builder.Append(aPerson.Name);
            builder.Append(" > ");
            builder.Append(seconds.ToString());
            builder.Append(" +0000");

            return (builder.ToString());
        }
    }
}
