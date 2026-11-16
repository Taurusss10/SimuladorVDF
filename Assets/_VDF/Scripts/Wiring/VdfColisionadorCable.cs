// NOTA: un MonoBehaviour por archivo. Ver E6 en VIABILIDAD.md.

using UnityEngine;

namespace VDF
{
    [ExecuteAlways]
    public class VdfColisionadorCable : MonoBehaviour
    {
        public enum Forma
        {
            /// <summary>Semiespacio: todo lo que quede por detrás del plano se empuja afuera.</summary>
            Plano,
            /// <summary>Caja alineada con el transform (admite rotación).</summary>
            Caja,
            Esfera,
            /// <summary>Cilindro a lo largo del eje local X.</summary>
            Cilindro
        }

        public Forma Tipo = Forma.Caja;

        [Tooltip("Medias dimensiones de la caja, en espacio local.")]
        public Vector3 Medidas = new Vector3(0.5f, 0.5f, 0.05f);

        [Tooltip("Radio de esfera o cilindro.")]
        public float Radio = 0.1f;

        [Tooltip("Media longitud del cilindro a lo largo de X.")]
        public float MediaLongitud = 0.1f;

        [Tooltip("Normal del plano, en espacio local.")]
        public Vector3 NormalPlano = Vector3.back;

        [Tooltip("Holgura extra para que el cable no se incruste visualmente.")]
        public float Margen = 0.002f;

        private void OnEnable()
        {
            if (!VdfCablePhysics.Colisionadores.Contains(this))
                VdfCablePhysics.Colisionadores.Add(this);
        }

        private void OnDisable()
        {
            VdfCablePhysics.Colisionadores.Remove(this);
        }

        /// <summary>
        /// Si el punto está dentro de la forma, devuelve true y la posición corregida
        /// sobre la superficie.
        /// </summary>
        public bool Empujar(Vector3 punto, float radioCable, out Vector3 corregido)
        {
            corregido = punto;
            float holgura = radioCable + Margen;

            switch (Tipo)
            {
                case Forma.Plano:
                {
                    Vector3 n = transform.TransformDirection(NormalPlano).normalized;
                    float d = Vector3.Dot(punto - transform.position, n);
                    if (d >= holgura) return false;

                    corregido = punto + n * (holgura - d);
                    return true;
                }

                case Forma.Esfera:
                {
                    Vector3 delta = punto - transform.position;
                    float d = delta.magnitude;
                    float r = Radio + holgura;
                    if (d >= r || d < 1e-6f) return false;

                    corregido = transform.position + delta * (r / d);
                    return true;
                }

                case Forma.Cilindro:
                {
                    Vector3 local = transform.InverseTransformPoint(punto);
                    float x = Mathf.Clamp(local.x, -MediaLongitud, MediaLongitud);
                    Vector3 eje = new Vector3(x, 0f, 0f);
                    Vector3 delta = local - eje;

                    float d = delta.magnitude;
                    float r = Radio + holgura;
                    if (d >= r || d < 1e-6f) return false;

                    corregido = transform.TransformPoint(eje + delta * (r / d));
                    return true;
                }

                default: // Caja
                {
                    Vector3 local = transform.InverseTransformPoint(punto);
                    Vector3 e = Medidas + Vector3.one * holgura;

                    // Fuera de la caja: no hay nada que corregir.
                    if (Mathf.Abs(local.x) >= e.x || Mathf.Abs(local.y) >= e.y || Mathf.Abs(local.z) >= e.z)
                        return false;

                    // Dentro: sale por la cara más próxima.
                    float px = e.x - Mathf.Abs(local.x);
                    float py = e.y - Mathf.Abs(local.y);
                    float pz = e.z - Mathf.Abs(local.z);

                    if (px <= py && px <= pz) local.x = Mathf.Sign(local.x) * e.x;
                    else if (py <= pz) local.y = Mathf.Sign(local.y) * e.y;
                    else local.z = Mathf.Sign(local.z) * e.z;

                    corregido = transform.TransformPoint(local);
                    return true;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;

            if (Tipo == Forma.Caja) Gizmos.DrawWireCube(Vector3.zero, Medidas * 2f);
            else if (Tipo == Forma.Esfera) Gizmos.DrawWireSphere(Vector3.zero, Radio);
            else if (Tipo == Forma.Cilindro)
            {
                Gizmos.DrawWireSphere(new Vector3(-MediaLongitud, 0f, 0f), Radio);
                Gizmos.DrawWireSphere(new Vector3(MediaLongitud, 0f, 0f), Radio);
            }
            else
            {
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 1f, 0.001f));
            }
        }
    }
}
