using System;
using System.Collections.Generic;

namespace MidtermAR
{
    // Native jobs outlive their UI. Keep their frozen identity and duplicate guard until completion.
    public sealed class RegistrationLifecycle
    {
        public sealed class Attempt
        {
            internal Attempt(int generation, string productKey, string productLabel)
            { Generation = generation; ProductKey = productKey; ProductLabel = productLabel; }
            internal int Generation { get; }
            public string ProductKey { get; }
            public string ProductLabel { get; }
        }

        private readonly HashSet<string> nativeImageProducts = new HashSet<string>(StringComparer.Ordinal);
        private Attempt pending;
        private int generation;
        public bool IsActive { get; private set; }
        public bool IsBusy => pending != null;
        public string ProductKey { get; private set; }
        public string ProductLabel { get; private set; }
        public bool HasNativeImage(string productKey) => productKey != null && nativeImageProducts.Contains(productKey);

        public bool CanBegin(string productKey) => !IsActive && !IsBusy
            && !string.IsNullOrWhiteSpace(productKey) && !HasNativeImage(productKey);

        public bool Begin(string productKey, string productLabel)
        {
            if (!CanBegin(productKey) || string.IsNullOrWhiteSpace(productLabel)) return false;
            generation++;
            ProductKey = productKey;
            ProductLabel = productLabel;
            IsActive = true;
            return true;
        }

        public Attempt StartJob()
        {
            if (!IsActive || IsBusy) throw new InvalidOperationException("No active registration can start a job.");
            pending = new Attempt(generation, ProductKey, ProductLabel);
            return pending;
        }

        public bool Complete(Attempt attempt, bool nativeSuccess)
        {
            if (attempt == null || !ReferenceEquals(pending, attempt)) return false;
            if (nativeSuccess) nativeImageProducts.Add(attempt.ProductKey);
            pending = null;
            var publish = nativeSuccess && IsActive && attempt.Generation == generation
                && attempt.ProductKey == ProductKey;
            if (publish) IsActive = false;
            return publish;
        }

        public void Cancel()
        {
            generation++;
            IsActive = false;
            ProductKey = ProductLabel = null;
            // pending deliberately remains alive until the native job is completed.
        }
    }
}
