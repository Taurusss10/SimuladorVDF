using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

namespace VDF
{
    public static class VdfAjustesMando
    {
        public const float RadioAgarreCercano = 0.03f;
        public const float AnguloConoRayo = 3f;
        public const float EsperaSocket = 0.25f;

        public static void Aplicar()
        {
            foreach (var nf in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include))
            {
                nf.allowHoveredActivate = true;
                nf.nearCasterSortingStrategy = NearFarInteractor.NearCasterSortingStrategy.ClosestPointOnCollider;
            }

            foreach (var esfera in Object.FindObjectsByType<SphereInteractionCaster>(FindObjectsInactive.Include))
                esfera.castRadius = RadioAgarreCercano;

            foreach (var curva in Object.FindObjectsByType<CurveInteractionCaster>(FindObjectsInactive.Include))
                curva.coneCastAngle = AnguloConoRayo;

            foreach (var ac in Object.FindObjectsByType<InteractionAttachController>(FindObjectsInactive.Include))
            {
                ac.useDistanceBasedVelocityScaling = false;
                ac.useMomentum = false;
                ac.useManipulationInput = false;
            }

            foreach (var s in Object.FindObjectsByType<XRSocketInteractor>(FindObjectsInactive.Include))
            {
                s.recycleDelayTime = EsperaSocket;
                s.showInteractableHoverMeshes = false;
            }

            // Las puntas de cable: encaje generoso al soltar, y al cogerlas con el rayo
            // VIENEN A LA MANO. Con «lejos», la punta se quedaba flotando a la distancia a la
            // que se apuntó y era casi imposible dejarla sobre un borne de 15 mm.
            foreach (var punta in Object.FindObjectsByType<VdfXrPlug>(FindObjectsInactive.Include))
            {
                VdfEncajeAlSoltar.Asegurar(punta.gameObject);
                var agarre = punta.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (agarre != null)
                    agarre.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Near;
            }

            VdfXrLocomocion.QuitarMovimientoContinuo();

            // Botones libres del mando: A, B y el joystick derecho programan el V1000 mientras
            // se le apunta, y X e Y manejan la lupa de la mano izquierda.
            VdfXrAtajosV1000.Asegurar();
            VdfXrLupa.Asegurar();
        }

        /// <summary>¿Está el mando como lo quiere el simulador? Para los tests.</summary>
        public static string Revisar()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var nf in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include))
                if (!nf.allowHoveredActivate) sb.Append(nf.name + ": el gatillo no activa lo apuntado. ");
            foreach (var esfera in Object.FindObjectsByType<SphereInteractionCaster>(FindObjectsInactive.Include))
                if (esfera.castRadius > RadioAgarreCercano + 0.001f) sb.Append("agarre cercano de " + esfera.castRadius + " m. ");
            foreach (var ac in Object.FindObjectsByType<InteractionAttachController>(FindObjectsInactive.Include))
                if (ac.useDistanceBasedVelocityScaling || ac.useMomentum || ac.useManipulationInput)
                    sb.Append(ac.name + ": el agarre tira del objeto hacia la mano. ");
            if (VdfXrLocomocion.HayMovimientoContinuo()) sb.Append("un joystick desliza al jugador. ");
            if (Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include).Length == 0)
                sb.Append("no hay NearFarInteractor: no se ha revisado nada. ");
            return sb.ToString();
        }
    }
}
