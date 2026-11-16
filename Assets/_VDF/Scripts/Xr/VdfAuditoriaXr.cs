using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace VDF
{
    public static class VdfAuditoriaXr
    {
        /// <summary>Altura de la mano respecto al suelo del puesto, de pie.</summary>
        public const float AlturaMano = 1.15f;

        /// <summary>Hasta dónde se considera que se apunta con comodidad.</summary>
        public const float AlcanceComodo = 1.6f;

        public static List<string> Auditar()
        {
            var problemas = new List<string>();

            var nearFar = Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include);
            if (nearFar.Length == 0) { problemas.Add("no hay NearFarInteractor en la escena"); return problemas; }

            // Lo que ve el rayo y lo que ve la esfera de agarre, sacado del propio rig.
            int mascaraRayo = 0, mascaraEsfera = 0, capasInteraccion = 0;
            bool rayoVeTriggers = false;
            foreach (var nf in nearFar)
            {
                capasInteraccion |= nf.interactionLayers.value;
                foreach (var c in nf.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (c == null) continue;
                    string n = c.GetType().Name;
                    if (n == "CurveInteractionCaster")
                    {
                        mascaraRayo |= LeerMascara(c, "m_RaycastMask");
                        rayoVeTriggers |= LeerEnum(c, "m_RaycastTriggerInteraction") == (int)QueryTriggerInteraction.Collide;
                    }
                    else if (n == "SphereInteractionCaster")
                        mascaraEsfera |= LeerMascara(c, "m_PhysicsLayerMask");
                }
            }

            var puestos = new List<Vector3>();
            foreach (var a in Object.FindObjectsByType<TeleportationAnchor>())
                puestos.Add(a.transform.position + Vector3.up * AlturaMano);
            var rig = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (rig != null) puestos.Add(rig.transform.position + Vector3.up * AlturaMano);

            foreach (var inter in Object.FindObjectsByType<XRBaseInteractable>())
            {
                if (inter is BaseTeleportationInteractable) continue;
                string nombre = Ruta(inter.transform);

                if ((inter.interactionLayers.value & capasInteraccion) == 0)
                    problemas.Add(nombre + ": sus capas de interacción no las usa el mando");

                Collider solido = null;
                foreach (var c in inter.colliders)
                    if (c != null && !c.isTrigger) { solido = c; break; }

                if (solido == null)
                {
                    problemas.Add(nombre + (rayoVeTriggers ? "" : ": solo tiene colliders trigger y el rayo los ignora"));
                    continue;
                }

                bool esAgarre = inter is XRGrabInteractable;
                int mascara = esAgarre ? (mascaraRayo | mascaraEsfera) : mascaraRayo;
                if ((mascara & (1 << solido.gameObject.layer)) == 0)
                    problemas.Add(nombre + ": está en la capa " + LayerMask.LayerToName(solido.gameObject.layer)
                                  + ", que el mando no mira");

                // Lo que se PULSA tiene que poder tocarse con el mando, no solo apuntarse.
                bool sePulsa = inter.GetComponent<VdfXrKeyPoke>() != null
                            || inter.GetComponent<VdfXrPulsador>() != null;
                if (sePulsa && inter.GetComponent<XRPokeFilter>() == null)
                    problemas.Add(nombre + ": sin XRPokeFilter, tocarlo con el mando no hace nada");

                // Las puntas de cable que esperan en la bandeja y los botones de paneles que
                // aún no se ven no se miden desde los puestos: no se apuntan desde ahí.
                if (esAgarre || !inter.gameObject.activeInHierarchy) continue;

                if (!SeAlcanzaDesdeAlgunPuesto(solido, puestos, mascaraRayo))
                    problemas.Add(nombre + ": desde ningún puesto se le apunta sin que algo lo tape");
            }

            return problemas;
        }

        /// <summary>Altura de los ojos de pie sobre el suelo del puesto.</summary>
        public const float AlturaOjos = 1.55f;

        /// <summary>
        /// Lo que mide la mayúscula de un texto vista desde el puesto más cercano, en grados.
        ///
        /// Es la medida de legibilidad que se usa en VR: unos 0,8–0,9° es el mínimo y 1,2°
        /// lo cómodo. Se mide sobre el texto ya maquetado (la primera mayúscula o cifra
        /// visible), no deduciéndola del tamaño de fuente.
        /// </summary>
        public static float AlturaAngular(TMPro.TextMeshPro t)
        {
            if (t == null || string.IsNullOrEmpty(t.text)) return 0f;
            t.ForceMeshUpdate();
            var info = t.textInfo;
            float cap = 0f;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible || !(char.IsUpper(c.character) || char.IsDigit(c.character))) continue;
                cap = (t.transform.TransformPoint(c.topLeft) - t.transform.TransformPoint(c.bottomLeft)).magnitude;
                break;
            }
            if (cap <= 0f) return 0f;

            Vector3 centro = t.transform.TransformPoint(t.textBounds.center);
            float d = float.MaxValue;
            foreach (var a in Object.FindObjectsByType<TeleportationAnchor>())
                d = Mathf.Min(d, Vector3.Distance(a.transform.position + Vector3.up * AlturaOjos, centro));
            if (d == float.MaxValue) return 0f;
            return 2f * Mathf.Atan(cap / (2f * d)) * Mathf.Rad2Deg;
        }

        /// <summary>Textos bajo una raíz que se ven por debajo de <paramref name="minimo"/> grados.</summary>
        public static List<string> TextosPequenos(string raiz, float minimo)
        {
            var lista = new List<string>();
            var go = GameObject.Find(raiz);
            if (go == null) { lista.Add(raiz + " no existe"); return lista; }
            foreach (var t in go.GetComponentsInChildren<TMPro.TextMeshPro>())
            {
                float a = AlturaAngular(t);
                if (a > 0f && a < minimo) lista.Add(raiz + "/" + t.name + " " + a.ToString("F2") + "°");
            }
            return lista;
        }

        private static bool SeAlcanzaDesdeAlgunPuesto(Collider objetivo, List<Vector3> puestos, int mascara)
        {
            Vector3 centro = objetivo.bounds.center;
            foreach (var p in puestos)
            {
                Vector3 d = centro - p;
                if (d.magnitude > AlcanceComodo) continue;

                RaycastHit hit;
                if (Physics.Raycast(p, d.normalized, out hit, d.magnitude + 0.05f, mascara,
                                    QueryTriggerInteraction.Ignore)
                    && hit.collider == objetivo)
                    return true;
            }
            return false;
        }

        private static int LeerMascara(Object o, string campo)
        {
            var f = Buscar(o.GetType(), campo);
            if (f == null) return 0;
            var v = f.GetValue(o);
            if (v is LayerMask) return ((LayerMask)v).value;
            return 0;
        }

        private static int LeerEnum(Object o, string campo)
        {
            var f = Buscar(o.GetType(), campo);
            return f == null ? -1 : System.Convert.ToInt32(f.GetValue(o));
        }

        private static System.Reflection.FieldInfo Buscar(System.Type t, string campo)
        {
            while (t != null)
            {
                var f = t.GetField(campo, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) return f;
                t = t.BaseType;
            }
            return null;
        }

        private static string Ruta(Transform t)
        {
            string r = t.name;
            if (t.parent != null) r = t.parent.name + "/" + r;
            return r;
        }
    }
}
