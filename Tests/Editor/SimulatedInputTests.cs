using NUnit.Framework;
using UnityEngine;

namespace Tykit.Tests
{
    public class SimulatedInputTests
    {
        [SetUp]
        public void Setup()
        {
            SimulatedInput.ReleaseAll();
        }

        [TearDown]
        public void TearDown()
        {
            SimulatedInput.ReleaseAll();
        }

        // --- Axis ---

        [Test]
        public void SetAxis_ReturnsSimulatedValue()
        {
            SimulatedInput.SetAxis("Horizontal", 1f);
            Assert.AreEqual(1f, SimulatedInput.GetAxis("Horizontal"));
        }

        [Test]
        public void GetAxisRaw_ReturnsNormalized()
        {
            SimulatedInput.SetAxis("Horizontal", 0.5f);
            Assert.AreEqual(1f, SimulatedInput.GetAxisRaw("Horizontal"));

            SimulatedInput.SetAxis("Horizontal", -0.3f);
            Assert.AreEqual(-1f, SimulatedInput.GetAxisRaw("Horizontal"));

            SimulatedInput.SetAxis("Horizontal", 0f);
            Assert.AreEqual(0f, SimulatedInput.GetAxisRaw("Horizontal"));
        }

        // --- Keys ---

        [Test]
        public void KeyDown_IsDetected()
        {
            SimulatedInput.KeyDown(KeyCode.Space);
            Assert.IsTrue(SimulatedInput.GetKeyDown(KeyCode.Space));
            Assert.IsTrue(SimulatedInput.GetKey(KeyCode.Space));
        }

        [Test]
        public void KeyUp_IsDetected()
        {
            SimulatedInput.KeyDown(KeyCode.Space);
            SimulatedInput.KeyUp(KeyCode.Space);
            Assert.IsTrue(SimulatedInput.GetKeyUp(KeyCode.Space));
            Assert.IsFalse(SimulatedInput.GetKey(KeyCode.Space));
        }

        [Test]
        public void EndFrame_ClearsDownAndUp()
        {
            SimulatedInput.KeyDown(KeyCode.Space);
            SimulatedInput.EndFrame();

            Assert.IsFalse(SimulatedInput.GetKeyDown(KeyCode.Space));
            Assert.IsTrue(SimulatedInput.GetKey(KeyCode.Space)); // still held
        }

        [Test]
        public void ReleaseAll_ClearsEverything()
        {
            SimulatedInput.SetAxis("Horizontal", 1f);
            SimulatedInput.KeyDown(KeyCode.Space);
            SimulatedInput.ReleaseAll();

            Assert.IsFalse(SimulatedInput.IsSimulating);
            Assert.IsFalse(SimulatedInput.GetKey(KeyCode.Space));
            // GetAxis falls through to real Input when not simulating
        }

        [Test]
        public void IsSimulating_FalseByDefault()
        {
            Assert.IsFalse(SimulatedInput.IsSimulating);
        }

        [Test]
        public void IsSimulating_TrueAfterInput()
        {
            SimulatedInput.KeyDown(KeyCode.A);
            Assert.IsTrue(SimulatedInput.IsSimulating);
        }

        [Test]
        public void MultipleKeys_Independent()
        {
            SimulatedInput.KeyDown(KeyCode.A);
            SimulatedInput.KeyDown(KeyCode.D);

            Assert.IsTrue(SimulatedInput.GetKey(KeyCode.A));
            Assert.IsTrue(SimulatedInput.GetKey(KeyCode.D));

            SimulatedInput.KeyUp(KeyCode.A);
            Assert.IsFalse(SimulatedInput.GetKey(KeyCode.A));
            Assert.IsTrue(SimulatedInput.GetKey(KeyCode.D));
        }
    }
}
