﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using net.r_eg.vsSBE.Events;
using net.r_eg.vsSBE.Exceptions;
using net.r_eg.vsSBE.SBEScripts.Components;
using net.r_eg.vsSBE.SBEScripts.Exceptions;

namespace vsSBETest
{
    /// <summary>
    ///This is a test class for InternalComponentTest and is intended
    ///to contain all InternalComponentTest Unit Tests
    ///</summary>
    [TestClass()]
    public class InternalComponentTest
    {
        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        /// <summary>
        ///A test for parse
        ///</summary>
        [TestMethod()]
        [ExpectedException(typeof(SyntaxIncorrectException))]
        public void parseTest()
        {
            InternalComponent target = new InternalComponent();
            target.parse("#[vsSBE events.Type.item(1)]");
        }

        /// <summary>
        ///A test for parse
        ///</summary>
        [TestMethod()]
        [ExpectedException(typeof(SyntaxIncorrectException))]
        public void parseTest2()
        {
            InternalComponent target = new InternalComponent();
            target.parse("vsSBE events.Type.item(1)");
        }

        /// <summary>
        ///A test for parse
        ///</summary>
        [TestMethod()]
        [ExpectedException(typeof(SubtypeNotFoundException))]
        public void parseTest3()
        {
            InternalComponent target = new InternalComponent();
            target.parse("[vsSBE NoExist.Type]");
        }

        /// <summary>
        ///A test for parse - stEvents
        ///</summary>
        [TestMethod()]
        [ExpectedException(typeof(SyntaxIncorrectException))]
        public void parseTest4()
        {
            InternalComponent target = new InternalComponent();
            target.parse("[vsSBE events.Type.item(name)]");
        }

        /// <summary>
        ///A test for parse - stEvents
        ///</summary>
        [TestMethod()]
        [ExpectedException(typeof(OperandNotFoundException))]
        public void parseTest5()
        {
            InternalComponent target = new InternalComponent();
            target.parse("[vsSBE events.Type.item(1).test]");
        }

        /// <summary>
        ///A test for parse - stEventItem - pEnabled
        ///</summary>
        [TestMethod()]
        public void parseTest6()
        {
            InternalComponentAccessor target = new InternalComponentAccessor();
            Assert.AreEqual("true", target.parse("[vsSBE events.Pre.item(1).Enabled]"));
            Assert.AreEqual("true", target.parse("[vsSBE events.Pre.item(\"Name1\").Enabled]"));
            Assert.AreEqual("false", target.parse("[vsSBE events.Pre.item(2).Enabled]"));
            Assert.AreEqual("false", target.parse("[vsSBE events.Pre.item(\"Name2\").Enabled]"));
        }

        /// <summary>
        ///A test for parse - stEventItem - pEnabled
        ///</summary>
        [TestMethod()]
        [ExpectedException(typeof(OperandNotFoundException))]
        public void parseTest7()
        {
            InternalComponentAccessor target = new InternalComponentAccessor();
            target.parse("[vsSBE events.Pre.item(1).Enabled = 1true]");
        }

        /// <summary>
        ///A test for parse - stEventItem - pEnabled
        ///</summary>
        [TestMethod()]
        public void parseTest8()
        {
            InternalComponentAccessor target = new InternalComponentAccessor();
            Assert.AreEqual("true", target.parse("[vsSBE events.Pre.item(1).Enabled]"));
            Assert.AreEqual(String.Empty, target.parse("[vsSBE events.Pre.item(1).Enabled = false]"));
            Assert.AreEqual("false", target.parse("[vsSBE events.Pre.item(1).Enabled]"));

            Assert.AreEqual("false", target.parse("[vsSBE events.Pre.item(\"Name2\").Enabled]"));
            Assert.AreEqual(String.Empty, target.parse("[vsSBE events.Pre.item(\"Name2\").Enabled = true]"));
            Assert.AreEqual("true", target.parse("[vsSBE events.Pre.item(\"Name2\").Enabled]"));
        }

        /// <summary>
        ///A test for parse - stEventItem - pStatus
        ///</summary>
        [TestMethod()]
        [ExpectedException(typeof(OperandNotFoundException))]
        public void parseTest9()
        {
            InternalComponentAccessor target = new InternalComponentAccessor();
            target.parse("[vsSBE events.Pre.item(1).Status.Has Errors]");
        }

        /// <summary>
        ///A test for parse - stEventItem - pStatus
        ///</summary>
        [TestMethod()]
        public void parseTest10()
        {
            InternalComponentAccessor target = new InternalComponentAccessor();
            Assert.AreEqual("false", target.parse("[vsSBE events.Pre.item(1).Status.HasErrors]"));
        }

        /// <summary>
        ///A test for parse - stEventItem - pStatus
        ///</summary>
        [TestMethod()]
        [ExpectedException(typeof(SubtypeNotFoundException))]
        public void parseTest11()
        {
            InternalComponentAccessor target = new InternalComponentAccessor();
            target.parse("[vsSBE events.Pre.item(1).Status.NotExistProp]");
        }

        private class InternalComponentAccessor: InternalComponent
        {
            private SBEEvent[] evt = null;

            protected override ISolutionEvent[] getEvent(SolutionEventType type)
            {
                if(evt != null) {
                    return evt;
                }

                evt = new SBEEvent[2]{ 
                    new SBEEvent(){
                        Name = "Name1", Enabled = true
                    },
                    new SBEEvent(){
                        Name = "Name2", Enabled = false
                    }
                };
                return evt;
            }
        }
    }
}
