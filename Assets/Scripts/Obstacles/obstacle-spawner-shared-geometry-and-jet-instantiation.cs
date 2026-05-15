using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Partial of ObstacleSpawner: low-level helpers shared between placement phases —
    /// AABB overlap rejection, jet GameObject instantiation, and the rect math used to
    /// reason about jet footprints.
    /// </summary>
    public partial class ObstacleSpawner
    {
        /// <summary>
        /// Spawn a jet at the given world position if it does not overlap any already-placed jet
        /// and is inside the spawn Y bounds. Returns false if the candidate is rejected.
        /// </summary>
        private bool TrySpawnAt(Vector2 pos, float scale)
        {
            if (pos.y < _spawnMinY || pos.y > _spawnMaxY) return false;
            Rect candidate = MakeJetRect(pos, scale, OverlapSeparationScale);
            foreach (var obs in _obstacles)
            {
                if (obs == null) continue;
                Rect existing = MakeJetRect(obs.transform.position, obs.transform.localScale.x, OverlapSeparationScale);
                if (candidate.Overlaps(existing)) return false;
            }
            CreateObstacle(pos, scale);
            return true;
        }

        /// <summary>Build the world-space AABB of a jet at given center+scale, padded by separation factor.</summary>
        private static Rect MakeJetRect(Vector2 center, float scale, float padding)
        {
            float w = SpriteWidthAtScale1 * scale * padding;
            float h = SpriteHeightAtScale1 * scale * padding;
            return new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);
        }

        /// <summary>
        /// Instantiate a jet GameObject: SpriteRenderer (protector.png), PolygonCollider2D from
        /// sprite alpha, exhaust trail + hover animation + defensive interceptor launcher.
        /// Falls back to a solid-square BoxCollider2D if the sprite is missing (pre-Setup-Scene state).
        /// </summary>
        private void CreateObstacle(Vector2 position, float size)
        {
            var go = new GameObject("Obstacle");
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.tag = GameConstants.TagGround;
            go.layer = GameConstants.DefaultLayer;
            go.transform.localScale = new Vector3(size, size, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _obstacleSprite != null ? _obstacleSprite : RuntimeSpriteFactory.GetSolidSprite();
            sr.color = _obstacleColor;
            sr.sortingLayerName = GameConstants.SortingLayerGameplay;

            if (_obstacleSprite != null)
            {
                go.AddComponent<PolygonCollider2D>();
                go.AddComponent<JetExhaustTrail>();
                go.AddComponent<JetHoverAnimation>();
                if (_interceptorSprite != null)
                {
                    JetInterceptorLauncher.SetInterceptorSprite(_interceptorSprite);
                    go.AddComponent<JetInterceptorLauncher>();
                }
            }
            else
            {
                go.AddComponent<BoxCollider2D>();
            }

            _obstacles.Add(go);
        }
    }
}
