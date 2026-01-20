using System.Diagnostics.CodeAnalysis;

namespace WBM_BackgroundProcessor.Models
{
    public class Result
    {
        protected Result(State myState, string errorMessage)
        {
            if (myState == State.Success && !string.IsNullOrEmpty(errorMessage))
                throw new InvalidOperationException("A successful result cannot have an error message.");

            if (myState != State.Success && string.IsNullOrEmpty(errorMessage))
                throw new InvalidOperationException("A failure result must have an error message.");

            ResultState = myState;
            ErrorMessage = errorMessage;

            switch (myState)
            {
                case State.Exception:
                    IsException = true;
                    IsFailure = true;
                    IsSuccess = false;
                    break;
                case State.Failure:
                    IsFailure = true;
                    IsException = false;
                    IsSuccess = false;
                    break;
                case State.Success:
                    IsException = false;
                    IsFailure = false;
                    IsSuccess = true;
                    break;
            }
        }

        public enum State
        {
            Success,            // did what it was supposed to do
            Failure,            // failed for logical reason
            Exception          // failed for exceptional reason
        }

        public State ResultState { get; }

        public bool IsSuccess { get; }

        public bool IsFailure { get; }

        public bool IsException { get; }

        public string ErrorMessage { get; }

        public static Result Success() => new(State.Success, "");
        public static Result Failure(string errorMessage) => new(State.Failure, errorMessage);

        public static Result Exception(string errorMessage) => new(State.Exception, errorMessage);


        public static Result<T> Success<T>(T value) => new(value, State.Success, "");
        public static Result<T> Failure<T>(string errorMessage) => new(default, State.Failure, errorMessage);

        public static Result<T> Exception<T>(string errorMessage) => new(default, State.Exception, errorMessage);

        public static Result<T> Create<T>(T? value) =>
       value is not null ? Success(value) : Failure<T>("Error.NullValue");

    }

    public class Result<T> : Result
    {
        private readonly T? _value;

        protected internal Result(T? value, State myState, string errorMessage) : base(myState, errorMessage)
            => _value = value;

        [NotNull]
        public T Value => _value! ?? throw new InvalidOperationException("Result has no value");

        public static implicit operator Result<T>(T? value) => Create(value);
    }
}
