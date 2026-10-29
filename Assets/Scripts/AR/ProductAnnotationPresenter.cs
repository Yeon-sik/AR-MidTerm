using System.Collections.Generic;
using UnityEngine;

namespace MidtermAR
{
    // 기본 정보판이 결정한 표시 게이트 안에서만 주석의 내용·객체·자세를 관리한다.
    public sealed class ProductAnnotationPresenter : MonoBehaviour
    {
        [SerializeField] private WorldProductAnnotation annotationPrefab;
        private ProductImageCatalog catalog;
        private ProductSnapshot boundProduct;
        private readonly List<(ProductAnnotationBinding binding, WorldProductAnnotation view)> views = new List<(ProductAnnotationBinding, WorldProductAnnotation)>();
        public int Count => views.Count;
        public void Configure(ProductImageCatalog value) { Clear(); catalog = value; }
        public void Bind(ProductSnapshot product)
        {
            if (ReferenceEquals(product, boundProduct)) return;
            Clear(); boundProduct = product;
            if (product == null || catalog == null || annotationPrefab == null) return;
            foreach (var annotation in catalog.AnnotationsFor(product.catalogProductId))
            {
                var text = ProductInsightCalculator.AnnotationText(product, annotation.kind);
                if (text == null) continue;
                var view = Instantiate(annotationPrefab, transform);
                view.name = annotation.id; view.SetContent(text);
                views.Add((annotation, view));
            }
        }
        public void ApplyPose(Transform image, ProductImageBinding binding, Rect basicPanel)
        {
            foreach (var entry in views)
                if (ProductAnnotationLayout.TryCreate(binding, entry.binding, basicPanel, out var layout)) entry.view.ApplyPose(image, binding, layout);
                else entry.view.InvalidatePose();
        }
        public void SetVisible(bool visible) { foreach (var entry in views) entry.view.SetVisible(visible); }
        private void Clear()
        {
            foreach (var entry in views)
            {
                if (entry.view == null) continue;
                entry.view.SetVisible(false);
                if (Application.isPlaying) Destroy(entry.view.gameObject); else DestroyImmediate(entry.view.gameObject);
            }
            views.Clear(); boundProduct = null;
        }
    }
}
