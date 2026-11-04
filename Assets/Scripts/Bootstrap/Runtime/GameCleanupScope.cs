using System;
using System.Collections.Generic;

namespace Game.Bootstrap
{
    /// <summary>Owned cleanup actions, released once in reverse registration order.</summary>
    public sealed class GameCleanupScope : IDisposable
    {
        private readonly Stack<Action> actions = new Stack<Action>();
        private bool disposed;

        public void Add(Action cleanup)
        {
            if (cleanup == null)
            {
                throw new ArgumentNullException(nameof(cleanup));
            }

            if (disposed)
            {
                throw new ObjectDisposedException(nameof(GameCleanupScope));
            }

            actions.Push(cleanup);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            List<Exception> errors = null;
            while (actions.Count > 0)
            {
                try
                {
                    actions.Pop()();
                }
                catch (Exception error)
                {
                    errors ??= new List<Exception>();
                    errors.Add(error);
                }
            }

            if (errors != null)
            {
                throw new AggregateException("One or more game cleanup actions failed.", errors);
            }
        }
    }
}
