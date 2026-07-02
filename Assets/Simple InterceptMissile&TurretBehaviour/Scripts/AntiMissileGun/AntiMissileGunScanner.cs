using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AntiMissileGunScanner : MonoBehaviour
{
	public enum Mode
	{
		NEAREST,
		FURTHEST
	}

	[Header("Settings")]
	[Tooltip("How often to scan in second")]
	public float ScanSpeed = 0.2f;

	[Tooltip("Scanner view angle")]
	[Range(0, 360)]
	public float ViewAngle = 360f;

	[Tooltip("Layers the scanner will detect")]
	public LayerMask Mask = ~0;

	[Tooltip("Get scanner range / or radius")]
	public float ScanRadius = 100f;

	[Tooltip("On or Off gizmos")]
	public bool ShowGizmos;

	[Tooltip("Turret Controller")]
	public AntiMissileGunController AntiMissileGunController;

	[Tooltip("Turret modes NOTE: Only working for anti missile gun controller")]
	public Mode TurretModes = Mode.NEAREST;

	[Header("Integração do Projeto")]
	[Tooltip("Se ativo, prioriza rastreadores globais de mísseis do projeto antes da varredura física")]
	public bool PriorizarMissileThreatTracker = true;

	[Tooltip("Ignora alvos da mesma raiz da torreta")]
	public bool IgnorarRaizPropria = true;

	[Tooltip("Se ativo, exige identidade inimiga quando o alvo possui IdentidadeUnidade")]
	public bool RespeitarTeamID = true;

	private readonly List<Transform> targetList = new List<Transform>();
	private readonly Collider[] buffer = new Collider[128];
	private IdentidadeUnidade minhaIdentidade;
	private Transform raizPropria;

	private void Start()
	{
		if (AntiMissileGunController == null)
		{
			Debug.LogWarning("[AntiMissileGunScanner] Nenhum controller encontrado.");
		}

		minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
		raizPropria = transform.root != null ? transform.root : transform;
		StartCoroutine(ScanIteration());
	}

	IEnumerator ScanIteration()
	{
		float intervalo = Mathf.Max(0.08f, ScanSpeed);
		while (true)
		{
			ScanForTarget();
			yield return new WaitForSeconds(intervalo);
		}
	}

	public Vector3 GetViewAngle(float angle)
	{
		float radiant = (angle + transform.eulerAngles.y) * Mathf.Deg2Rad;
		return new Vector3(Mathf.Sin(radiant), 0, Mathf.Cos(radiant));
	}

	private void OnDrawGizmos()
	{
		if (!ShowGizmos) return;

		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(transform.position, ScanRadius);
		Gizmos.DrawLine(transform.position, transform.position + GetViewAngle(ViewAngle / 2) * ScanRadius);
		Gizmos.DrawLine(transform.position, transform.position + GetViewAngle(-ViewAngle / 2) * ScanRadius);

		Gizmos.color = Color.red;
		if (targetList.Count == 0) return;
		foreach (Transform target in targetList)
		{
			if (target == null) continue;
			Gizmos.DrawLine(transform.position, target.position);
		}
	}

	public void ScanForTarget()
	{
		if (AntiMissileGunController == null)
		{
			return;
		}

		targetList.Clear();
		Transform threat = PriorizarAmeacaGlobal();
		if (threat != null)
		{
			SetTargetGun(threat);
			return;
		}

		int hitCount = Physics.OverlapSphereNonAlloc(transform.position, ScanRadius, buffer, Mask, QueryTriggerInteraction.Collide);
		float melhorPontuacao = float.PositiveInfinity;
		float piorPontuacao = float.NegativeInfinity;
		Transform melhorAlvo = null;

		for (int i = 0; i < hitCount; i++)
		{
			Collider hit = buffer[i];
			if (hit == null) continue;

			Transform alvo = ResolverTransformPrincipal(hit.transform);
			if (!EhAlvoValido(alvo))
			{
				buffer[i] = null;
				continue;
			}

			float distancia = Vector3.Distance(transform.position, alvo.position);
			if (distancia > ScanRadius)
			{
				buffer[i] = null;
				continue;
			}

			Vector3 dirTotarget = (alvo.position - transform.position).normalized;
			float angulo = Vector3.Angle(transform.forward, dirTotarget);
			if (angulo > ViewAngle * 0.5f)
			{
				buffer[i] = null;
				continue;
			}

			targetList.Add(alvo);

			float score = CalcularPrioridade(alvo, distancia);
			if (TurretModes == Mode.NEAREST)
			{
				if (score < melhorPontuacao)
				{
					melhorPontuacao = score;
					melhorAlvo = alvo;
				}
			}
			else
			{
				if (score > piorPontuacao)
				{
					piorPontuacao = score;
					melhorAlvo = alvo;
				}
			}

			buffer[i] = null;
		}

		if (melhorAlvo != null)
		{
			SetTargetGun(melhorAlvo);
			return;
		}

		SetTargetGun(null);
	}

	private Transform PriorizarAmeacaGlobal()
	{
		if (!PriorizarMissileThreatTracker)
		{
			return null;
		}

		int team = minhaIdentidade != null ? minhaIdentidade.teamID : -1;
		Transform alvo = MissileThreatTracker.EncontrarAmeacaMaisProxima(
			transform.position,
			ScanRadius,
			team,
			IgnorarRaizPropria ? raizPropria : null,
			1.25f,
			6f);

		if (!EhAlvoValido(alvo))
		{
			return null;
		}

		return alvo;
	}

	private float CalcularPrioridade(Transform alvo, float distancia)
	{
		float score = distancia;
		MissileThreatTracker tracker = alvo.GetComponentInParent<MissileThreatTracker>();
		if (tracker != null)
		{
			score -= 35f;
		}

		Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
		if (rb != null && rb.linearVelocity.sqrMagnitude > 25f)
		{
			Vector3 direcao = rb.linearVelocity.normalized;
			Vector3 paraMim = (transform.position - alvo.position).normalized;
			score -= Vector3.Dot(direcao, paraMim) * 40f;
		}

		return score;
	}

	private bool EhAlvoValido(Transform alvo)
	{
		if (alvo == null) return false;
		if (!alvo.gameObject.activeInHierarchy) return false;

		Transform raiz = alvo.root != null ? alvo.root : alvo;
		if (IgnorarRaizPropria && raizPropria != null && raiz == raizPropria) return false;

		if (!RespeitarTeamID || minhaIdentidade == null)
		{
			return true;
		}

		MissileThreatTracker tracker = alvo.GetComponentInParent<MissileThreatTracker>();
		if (tracker != null && tracker.TeamOrigem != -1)
		{
			return tracker.TeamOrigem != minhaIdentidade.teamID;
		}

		IdentidadeUnidade idAlvo = alvo.GetComponentInParent<IdentidadeUnidade>();
		if (idAlvo == null) return true;

		return idAlvo.teamID != minhaIdentidade.teamID;
	}

	private Transform ResolverTransformPrincipal(Transform alvo)
	{
		if (alvo == null) return null;

		SistemaDeDanos vida = alvo.GetComponentInParent<SistemaDeDanos>();
		if (vida != null) return vida.transform;

		Projetil projetil = alvo.GetComponentInParent<Projetil>();
		if (projetil != null) return projetil.transform;

		Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
		if (rb != null) return rb.transform;

		IdentidadeUnidade identidade = alvo.GetComponentInParent<IdentidadeUnidade>();
		if (identidade != null) return identidade.transform;

		return alvo.root != null ? alvo.root : alvo;
	}

	private void SetTargetGun(Transform targetPosition)
	{
		AntiMissileGunController.SetTargetGun(targetPosition);
	}
}
