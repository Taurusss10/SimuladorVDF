using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Juzga si un paso está cumplido, mirando el estado real del simulador.
    ///
    /// C# puro: se puede probar el guion entero de las cinco prácticas en EditMode, sin
    /// escena. Es la misma decisión que se tomó con el variador y el PLC, y por el mismo
    /// motivo: lo que no se puede probar barato, no se prueba.
    ///
    /// Un paso **nunca se marca a mano**. Si el alumno hace las cosas en otro orden, la guía
    /// lo refleja; si rompe algo que ya tenía bien, el paso se desmarca, salvo que sea un
    /// logro (ver <see cref="VdfPasoPractica.EsLogro"/>).
    /// </summary>
    public static class VdfEvaluadorPasos
    {
        /// <summary>Tolerancia por defecto al comparar valores de parámetros.</summary>
        public const float ToleranciaPorDefecto = 0.005f;

        public static bool Cumple(VdfPasoPractica paso, VdfContextoPractica ctx)
        {
            if (paso == null || ctx == null) return false;

            switch (paso.Condicion)
            {
                case VdfCondicion.Manual:
                    // Lo marca el alumno al leerlo; el runner lo registra como identificado.
                    return ctx.Identificados.Contains(ClaveManual(paso));

                // --- Práctica 1 -----------------------------------------------------------
                case VdfCondicion.ComponenteIdentificado:
                    return !string.IsNullOrEmpty(paso.Objetivo) &&
                           ctx.Identificados.Contains(paso.Objetivo);

                case VdfCondicion.GrupoIdentificado:
                    return GrupoCompleto(paso.Objetivo, ctx);

                // --- Práctica 2 -----------------------------------------------------------
                case VdfCondicion.ParametroEnValor:
                    return ParametroEnValor(paso, ctx);

                case VdfCondicion.ConfiguracionOperable:
                    return ctx.Vfd != null && ctx.Vfd.ConfiguracionEsOperable();

                case VdfCondicion.ConfiguracionSinAvisos:
                    return ctx.Vfd != null && ctx.Vfd.ValidarConfiguracion().Count == 0;

                // --- Práctica 3 -----------------------------------------------------------
                case VdfCondicion.BornesUnidos:
                    return BornesUnidos(paso.Objetivo, ctx);

                case VdfCondicion.CableadoCorrecto:
                    return ctx.InformeCableado != null && ctx.InformeCableado.EsCorrecto;

                case VdfCondicion.SinCablesSobrantes:
                    return ctx.InformeCableado != null && ctx.InformeCableado.Sobrantes.Count == 0
                           && ctx.InformeCableado.Faltantes.Count == 0;

                // --- Práctica 4 -----------------------------------------------------------
                case VdfCondicion.VariadorConTension:
                    return ctx.Vfd != null && ctx.Vfd.Alimentado;

                case VdfCondicion.SistemaArrancado:
                    return ctx.Arranco;

                case VdfCondicion.NivelAlcanzado:
                    return ctx.NivelMaximoAlcanzado >= Mathf.RoundToInt(paso.Valor);

                case VdfCondicion.BajoHastaNivel1:
                    return ctx.BajoHastaUno;

                case VdfCondicion.ParadoTrasOperar:
                    return ctx.ParoDespuesDeOperar;

                // --- Práctica 5 -----------------------------------------------------------
                case VdfCondicion.EmergenciaAccionada:
                    return ctx.EmergenciaAccionada;

                case VdfCondicion.EmergenciaRearmada:
                    return ctx.EmergenciaRearmada;

                case VdfCondicion.VariadorEnFalla:
                    return ctx.Vfd != null && ctx.Vfd.Falla != VfdFault.Ninguna;

                case VdfCondicion.VariadorEstuvoEnFalla:
                    return ctx.HuboFalla;

                case VdfCondicion.FallaReparada:
                    // Sin avería provocada no hay nada que reparar, así que el paso se da
                    // por cumplido en lugar de bloquear la práctica.
                    return ctx.FallaActiva == null ||
                           ctx.FallaActiva.Reparada(ctx.Vfd, ctx.Grafo);

                case VdfCondicion.FallaDiagnosticada:
                    return ctx.FallaActiva != null &&
                           ctx.Identificados.Contains(ctx.FallaActiva.Id);

                case VdfCondicion.VariadorInicializado:
                    return ctx.VariadorInicializado;

                case VdfCondicion.EmergenciaConMotorGirando:
                    return ctx.EmergenciaConMotorGirando;

                case VdfCondicion.FallaReparadaYProbada:
                    if (ctx.FallaActiva == null) return true;
                    return ctx.FallaActiva.Reparada(ctx.Vfd, ctx.Grafo)
                           && ctx.NivelMaximoTrasReparar >= PlcFanLogic.NivelMaximo;

                case VdfCondicion.ParametroRevisado:
                    return !string.IsNullOrEmpty(paso.Objetivo)
                           && ctx.ParametrosVistos.Contains(paso.Objetivo)
                           && ParametroEnValor(paso, ctx);
            }

            return false;
        }

        /// <summary>
        /// Clave con la que se registra un paso manual como «leído». Se usa la instrucción
        /// porque un paso manual no tiene objetivo propio.
        /// </summary>
        public static string ClaveManual(VdfPasoPractica paso)
        {
            if (paso == null) return null;
            return !string.IsNullOrEmpty(paso.Objetivo) ? paso.Objetivo : "MANUAL:" + paso.Instruccion;
        }

        private static bool ParametroEnValor(VdfPasoPractica paso, VdfContextoPractica ctx)
        {
            if (ctx.Vfd == null || string.IsNullOrEmpty(paso.Objetivo)) return false;

            VfdParameterDef def;
            if (!VfdParameterTable.TryGet(paso.Objetivo, out def)) return false;

            float tol = paso.Tolerancia > 0f ? paso.Tolerancia : ToleranciaPorDefecto;
            return Mathf.Abs(ctx.Vfd.Valor(paso.Objetivo) - paso.Valor) <= tol;
        }

        /// <summary>
        /// Dos bornes separados por "|". Se pregunta al grafo si quedan al mismo potencial,
        /// no si hay un cable directo: unir A-B-C es eléctricamente lo mismo que A-C-B, y el
        /// simulador no debe suspender un cableado que en el tablero real funciona.
        /// </summary>
        private static bool BornesUnidos(string objetivo, VdfContextoPractica ctx)
        {
            if (ctx.Grafo == null || string.IsNullOrEmpty(objetivo)) return false;

            int barra = objetivo.IndexOf('|');
            if (barra <= 0 || barra >= objetivo.Length - 1) return false;

            string a = objetivo.Substring(0, barra).Trim();
            string b = objetivo.Substring(barra + 1).Trim();
            return ctx.Grafo.MismaRed(a, b);
        }

        private static bool GrupoCompleto(string grupo, VdfContextoPractica ctx)
        {
            if (string.IsNullOrEmpty(grupo)) return false;

            System.Collections.Generic.List<string> miembros;
            if (!ctx.Grupos.TryGetValue(grupo, out miembros) || miembros.Count == 0) return false;

            for (int i = 0; i < miembros.Count; i++)
                if (!ctx.Identificados.Contains(miembros[i])) return false;

            return true;
        }

        /// <summary>
        /// Cuántos elementos del grupo lleva identificados, para poder mostrar «3 de 7».
        /// Devuelve -1 si el grupo no existe.
        /// </summary>
        public static int ProgresoDeGrupo(string grupo, VdfContextoPractica ctx, out int total)
        {
            total = 0;
            if (ctx == null || string.IsNullOrEmpty(grupo)) return -1;

            System.Collections.Generic.List<string> miembros;
            if (!ctx.Grupos.TryGetValue(grupo, out miembros)) return -1;

            total = miembros.Count;
            int n = 0;
            for (int i = 0; i < miembros.Count; i++)
                if (ctx.Identificados.Contains(miembros[i])) n++;
            return n;
        }
    }
}
