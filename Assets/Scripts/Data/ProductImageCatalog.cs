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
        public ProductAnnotationBinding[] productAnnotations;
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
        public float productWidthMetres; // 크롭 폭과 구분한 상품 공통 영역의 폭
        public string productWidthSource;
        public float productHeightMetres;
        public string productHeightSource;
    }

    [Preserve, Serializable]
    public sealed class ProductAnnotationBinding
    {
        public string catalogProductId;
        public string id;
        public string kind;
        public float anchorXMetres;
        public float anchorZMetres;
        public string side;
        public string pixelEvidence;
    }

    // 참조 이미지 이름을 상품 ID에 연결한다. 같은 상품의 여러 크롭을 허용한다.
    public sealed class ProductImageCatalog
    {
        private readonly Dictionary<string, ProductImageBinding> images = new Dictionary<string, ProductImageBinding>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ProductAnnotationBinding>> annotations = new Dictionary<string, List<ProductAnnotationBinding>>(StringComparer.Ordinal);

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
            var widths = new Dictionary<string, float>(StringComparer.Ordinal);
            var heights = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var image in document.images)
            {
                if (image == null || !products.TryFindById(image.catalogProductId, out _)
                    || string.IsNullOrWhiteSpace(image.referenceImageName) || string.IsNullOrWhiteSpace(image.displayName)
                    || !names.Add(image.referenceImageName) || !Finite(image.offsetXMetres) || !Finite(image.offsetZMetres))
                    throw new FormatException("상품 이미지의 상품 ID·이름·오프셋이 없거나 중복되었습니다.");
                images.Add(image.referenceImageName, image);
                if (!Finite(image.productWidthMetres) || image.productWidthMetres < 0
                    || !image.developmentOnly && (image.productWidthMetres <= 0 || string.IsNullOrWhiteSpace(image.productWidthSource)))
                    throw new FormatException("상품 공통 폭과 실측 근거가 필요합니다.");
                if (widths.TryGetValue(image.catalogProductId, out var width) && width != image.productWidthMetres)
                    throw new FormatException("같은 상품의 크롭은 같은 공통 폭을 사용해야 합니다.");
                widths[image.catalogProductId] = image.productWidthMetres;
                if (!Finite(image.productHeightMetres) || image.productHeightMetres < 0
                    || image.productHeightMetres > 0 && string.IsNullOrWhiteSpace(image.productHeightSource)
                    || heights.TryGetValue(image.catalogProductId, out var height) && height != image.productHeightMetres)
                    throw new FormatException("상품 공통 높이와 사진 환산 근거가 없거나 크롭끼리 다릅니다.");
                heights[image.catalogProductId] = image.productHeightMetres;
            }
            foreach (var annotation in document.productAnnotations ?? Array.Empty<ProductAnnotationBinding>())
            {
                if (annotation == null || !products.TryFindById(annotation.catalogProductId, out _)
                    || !widths.TryGetValue(annotation.catalogProductId, out var width) || width <= 0
                    || !heights.TryGetValue(annotation.catalogProductId, out var height) || height <= 0
                    || string.IsNullOrWhiteSpace(annotation.id) || string.IsNullOrWhiteSpace(annotation.pixelEvidence)
                    || annotation.kind != "servingPrice" && annotation.kind != "observationSummary"
                    || annotation.side != "left" && annotation.side != "top"
                    || !Finite(annotation.anchorXMetres) || !Finite(annotation.anchorZMetres)
                    || Math.Abs(annotation.anchorXMetres) > width * .5f || Math.Abs(annotation.anchorZMetres) > height * .5f)
                    throw new FormatException("포장 주석의 상품·종류·위치·픽셀 근거가 올바르지 않습니다.");
                if (!annotations.TryGetValue(annotation.catalogProductId, out var list))
                { list = new List<ProductAnnotationBinding>(); annotations.Add(annotation.catalogProductId, list); }
                if (list.Exists(a => a.id == annotation.id)) throw new FormatException("상품 안에서 주석 ID가 중복됩니다.");
                list.Add(annotation);
            }
        }

        public IReadOnlyList<ProductAnnotationBinding> AnnotationsFor(string catalogProductId)
            => catalogProductId != null && annotations.TryGetValue(catalogProductId, out var list) ? list : (IReadOnlyList<ProductAnnotationBinding>)Array.Empty<ProductAnnotationBinding>();

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
