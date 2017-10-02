using System;
using System.Windows.Threading;
using EditorUtils.Implementation.Utilities;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;

namespace EditorUtils.UnitTest
{
    public sealed class ProtectedOperationsTest
    {
        private readonly Mock<IExtensionErrorHandler> _errorHandler;
        private readonly ProtectedOperations _protectedOperationsRaw;
        private readonly IProtectedOperations _protectedOperations;

        public ProtectedOperationsTest()
        {
            _errorHandler = new Mock<IExtensionErrorHandler>(MockBehavior.Strict);
            _protectedOperationsRaw = new ProtectedOperations(_errorHandler.Object);
            _protectedOperations = _protectedOperationsRaw;
        }

        /// <summary>
        /// Verify the returned action will execute the original one
        /// </summary>
        [Fact]
        public void GetProtectedAction_Standard()
        {
            var didRun = false;
            var protectedAction = _protectedOperations.GetProtectedAction(delegate { didRun = true; });
            protectedAction();
            Assert.True(didRun);
        }

        /// <summary>
        /// Verify that when the original action throws that it is passed on to the
        /// listed IExtensionErrorHandlers
        /// </summary>
        [Fact]
        public void GetProtectedAction_Throws()
        {
            var exception = new Exception("hello world");
            _errorHandler.Setup(x => x.HandleError(It.IsAny<object>(), exception)).Verifiable();
            var protectedAction = _protectedOperations.GetProtectedAction(delegate { throw exception; });
            protectedAction();
            _errorHandler.Verify();
        }

        /// <summary>
        /// Verify the returned EventHandler will execute the original one
        /// </summary>
        [Fact]
        public void GetProtectedEventHandler_Standard()
        {
            var didRun = false;
            var protectedEventHandler = _protectedOperations.GetProtectedEventHandler(delegate { didRun = true; });
            protectedEventHandler(null, EventArgs.Empty);
            Assert.True(didRun);
        }

        /// <summary>
        /// Verify that when the original EventHandler throws that it is passed on to the
        /// listed IExtensionErrorHandlers
        /// </summary>
        [Fact]
        public void GetProtectedEventHandler_Throws()
        {
            var exception = new Exception("hello world");
            _errorHandler.Setup(x => x.HandleError(It.IsAny<object>(), exception)).Verifiable();
            var protectedEventHandler = _protectedOperations.GetProtectedEventHandler(delegate { throw exception; });
            protectedEventHandler(null, EventArgs.Empty);
            _errorHandler.Verify();
        }

        /// <summary>
        /// Verify that the BeginInvoke will actually schedule the original action
        /// </summary>
        [Fact]
        public void BeginInvoke_Priority_Standard()
        {
            var didRun = false;
            _protectedOperations.BeginInvoke(delegate { didRun = true; }, DispatcherPriority.Normal);
            Dispatcher.CurrentDispatcher.DoEvents();
            Assert.True(didRun);
        }

        /// <summary>
        /// Verify that when an exception is thrown during processing that it makes it's way to the 
        /// IExtensionErrorHandler
        /// </summary>
        [Fact]
        public void BeginInvoke_Priority_Throws()
        {
            var exception = new Exception("hello world");
            _errorHandler.Setup(x => x.HandleError(It.IsAny<object>(), exception)).Verifiable();
            _protectedOperations.BeginInvoke(delegate { throw exception; }, DispatcherPriority.Normal);
            Dispatcher.CurrentDispatcher.DoEvents();
            _errorHandler.Verify();
        }

        /// <summary>
        /// Verify that the BeginInvoke will actually schedule the original action
        /// </summary>
        [Fact]
        public void BeginInvoke_NoPriority_Standard()
        {
            var didRun = false;
            _protectedOperations.BeginInvoke(delegate { didRun = true; });
            Dispatcher.CurrentDispatcher.DoEvents();
            Assert.True(didRun);
        }

        /// <summary>
        /// Verify that when an exception is thrown during processing that it makes it's way to the 
        /// IExtensionErrorHandler
        /// </summary>
        [Fact]
        public void BeginInvoke_NoPriority_Throws()
        {
            var exception = new Exception("hello world");
            _errorHandler.Setup(x => x.HandleError(It.IsAny<object>(), exception)).Verifiable();
            _protectedOperations.BeginInvoke(delegate { throw exception; });
            Dispatcher.CurrentDispatcher.DoEvents();
            _errorHandler.Verify();
        }
    }
}

