using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR
{
    [Preserve, Serializable]
    public sealed class ProductImageDocument
    {
        public int schemaVersion;
        public ProductImageBinding[] images;
    }

    [Preserve, Serializable]
    public sealed class ProductImageBinding
    {
        public string catalogProductId;
        public string referenceImageName;
        public string displayName;
        public bool developmentOnly;
        public float offsetXMetres; // 크롭 중심에서 상품 공통 원점까지의 이미지 로컬 오프셋
        public float offsetZMetres;
    }

    // 참조 이미지 이름을 상품 ID에 연결한다. 같은 상품의 여러 크롭을 허용한다.
    public sealed class ProductImageCatalog
    {
        private readonly Dictionary<string, ProductImageBinding> images = new Dictionary<string, ProductImageBinding>(StringComparer.Ordinal);

        public ProductImageCatalog(string json, ProductCatalog products)
        {
            ProductImageDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<ProductImageDocument>(json,
                    new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
            }
            catch (JsonException error) { throw new FormatException("상품 이미지 연결 JSON을 확인해 주세요.", error); }
            if (products == null || document == null || document.schemaVersion != 2 || document.images == null)
                throw new FormatException("상품 이미지 연결 데이터가 올바르지 않습니다.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var image in document.images)
            {
                if (image == null || !products.TryFindById(image.catalogProductId, out _)
                    || string.IsNullOrWhiteSpace(image.referenceImageName) || string.IsNullOrWhiteSpace(image.displayName)
                    || !names.Add(image.referenceImageName) || !Finite(image.offsetXMetres) || !Finite(image.offsetZMetres))
                    throw new FormatException("상품 이미지의 상품 ID·이름·오프셋이 없거나 중복되었습니다.");
                images.Add(image.referenceImageName, image);
            }
        }

        public bool TryFind(ProductSnapshot product, out ProductImageBinding binding)
        {
            binding = null;
            if (product == null) return false;
            foreach (var image in images.Values)
                if (image.catalogProductId == product.catalogProductId) { binding = image; return true; }
            return false;
        }

        public bool TryFindByImage(string name, out ProductImageBinding binding)
        {
            binding = null;
            return name != null && images.TryGetValue(name, out binding);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void ValidateLibrary(IReferenceImageLibrary library)
        {
            if (library == null) throw new FormatException("Reference Image Library가 없습니다.");
            var registered = new Dictionary<string, XRReferenceImage>(StringComparer.Ordinal);
            for (var index = 0; index < library.count; index++)
            {
                var image = library[index];
                if (string.IsNullOrWhiteSpace(image.name) || registered.ContainsKey(image.name))
                    throw new FormatException("Reference Image Library의 이미지 이름이 비어 있거나 중복되었습니다.");
                registered.Add(image.name, image);
            }
            foreach (var binding in images.Values)
            {
                if (!registered.TryGetValue(binding.referenceImageName, out var reference))
                    throw new FormatException("등록되지 않은 참조 이미지: " + binding.referenceImageName);
                if (!binding.developmentOnly && (!reference.specifySize || reference.size.x <= 0 || reference.size.y <= 0
                    || float.IsNaN(reference.size.x) || float.IsInfinity(reference.size.x)
                    || float.IsNaN(reference.size.y) || float.IsInfinity(reference.size.y)))
                    throw new FormatException("상품 포장 이미지에는 실측한 Physical Size가 필요합니다: " + binding.referenceImageName);
            }
        }
    }
}
