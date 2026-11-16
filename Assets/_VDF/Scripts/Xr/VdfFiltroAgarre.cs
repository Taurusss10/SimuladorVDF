// NOTA IMPORTANTE DE UNITY
// Cada MonoBehaviour tiene que vivir en un archivo .cs con SU MISMO NOMBRE.
// Ver E6 en VIABILIDAD.md: esto costo 71 componentes rotos y un crash.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VDF
{
    public class VdfFiltroAgarre : MonoBehaviour, IXRSelectFilter
    {
        public VdfXrWiring Cableado;

        public bool canProcess { get { return isActiveAndEnabled; } }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor is XRSocketInteractor) return true;
            return Cableado == null || Cableado.SePuedenCoger;
        }

        /// <summary>Lo engancha a la punta si aún no lo está. Idempotente.</summary>
        public static void Asegurar(GameObject punta, VdfXrWiring cableado)
        {
            var agarre = punta.GetComponent<XRGrabInteractable>();
            if (agarre == null) return;

            var filtro = punta.GetComponent<VdfFiltroAgarre>();
            if (filtro == null) filtro = punta.AddComponent<VdfFiltroAgarre>();
            filtro.Cableado = cableado;

            // La lista de filtros vive en memoria: hay que registrarse en cada arranque.
            // Quitar y volver a poner lo deja exactamente una vez, sin duplicados.
            agarre.selectFilters.Remove(filtro);
            agarre.selectFilters.Add(filtro);
        }

        /// <summary>¿La punta tiene el filtro puesto y registrado en su agarre?</summary>
        public static bool EstaRegistrado(GameObject punta)
        {
            var agarre = punta.GetComponent<XRGrabInteractable>();
            var filtro = punta.GetComponent<VdfFiltroAgarre>();
            if (agarre == null) return true;
            if (filtro == null) return false;

            var lista = new System.Collections.Generic.List<IXRSelectFilter>();
            agarre.selectFilters.GetAll(lista);
            return lista.Contains(filtro);
        }
    }
}
