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
        public string barcode;
        public string referenceImageName;
        public string displayName;
        public bool developmentOnly;
    }

    // 상품 식별은 EAN, 자세는 Reference Image Library가 담당한다.
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
            if (products == null || document == null || document.schemaVersion != 1 || document.images == null || document.images.Length == 0)
                throw new FormatException("상품 이미지 연결 데이터가 올바르지 않습니다.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var image in document.images)
            {
                if (image == null || !products.TryFind(image.barcode, out var product) || product.barcode != image.barcode
                    || string.IsNullOrWhiteSpace(image.referenceImageName) || string.IsNullOrWhiteSpace(image.displayName)
                    || images.ContainsKey(image.barcode) || !names.Add(image.referenceImageName))
                    throw new FormatException("상품 이미지의 바코드·이름이 없거나 중복되었습니다.");
                images.Add(image.barcode, image);
            }
        }

        public bool TryFind(ProductSnapshot product, out ProductImageBinding binding)
        {
            binding = null;
            return product != null && images.TryGetValue(product.barcode, out binding);
        }

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
