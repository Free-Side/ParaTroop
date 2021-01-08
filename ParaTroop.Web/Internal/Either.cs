using System;

namespace ParaTroop.Web.Internal {
    public readonly struct Either<TLeft, TRight> {
        private readonly Boolean isLeft;
        private readonly TLeft leftValue;
        private readonly TRight rightValue;

        public EitherType Type => isLeft ? EitherType.Left : EitherType.Right;

        public TLeft Left {
            get {
                if (!this.isLeft) {
                    throw new InvalidOperationException("Attempted to read the left value of a Right-wise Either.");
                }

                return this.leftValue;
            }
        }

        public TRight Right {
            get {
                if (this.isLeft) {
                    throw new InvalidOperationException("Attempted to read the Right value of a Left-wise Either.");
                }

                return this.rightValue;
            }
        }

        public Either(TLeft left) {
            this.isLeft = true;
            this.leftValue = left;
            this.rightValue = default;
        }

        public Either(TRight right) {
            this.isLeft = false;
            this.leftValue = default;
            this.rightValue = right;
        }

        public override Boolean Equals(Object obj) {
            return obj is Either<TLeft,TRight> other &&  this.Equals(other);
        }

        public Boolean Equals(Either<TLeft, TRight> other) {
            return this.isLeft ?
                other.isLeft && Equals(this.leftValue, other.leftValue) :
                !other.isLeft && Equals(this.rightValue, other.rightValue);
        }

        public override Int32 GetHashCode() {
            return this.isLeft ?
                HashCode.Combine(typeof(Either<TLeft, TRight>), this.leftValue) :
                HashCode.Combine(typeof(Either<TLeft, TRight>), this.rightValue);
        }

        public T WithValue<T>(Func<TRight, T> withRight, Func<TLeft, T> withLeft) {
            return this.isLeft ?
                withLeft(this.leftValue) :
                withRight(this.rightValue);
        }

        public static explicit operator TLeft(Either<TLeft, TRight> either) {
            return either.Left;
        }

        public static explicit operator TRight(Either<TLeft, TRight> either) {
            return either.Right;
        }

        public static implicit operator Either<TLeft, TRight>(TLeft left) {
            return new Either<TLeft, TRight>(left);
        }

        public static implicit operator Either<TLeft, TRight>(TRight right) {
            return new Either<TLeft, TRight>(right);
        }
    }

    public enum EitherType {
        Left,
        Right
    }
}
