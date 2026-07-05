using System.Collections.Generic;
using UnityEngine;

public class AntiMissileGunController : MonoBehaviour
{
	[Header("Turret Settings")]
	[Tooltip("Pivot for horizontal rotation")]
	public Transform HorizontalPivot;

	[Tooltip("Pivot for vertical rotation")]
	public Transform VerticalPivot;

	[Header("Horizontal Rotation Settings")]
	[Tooltip("If you want to limit horizontal turret rotation")]
	public bool HorizontalRotationLimit;

	[Tooltip("Right rotation limit")]
	[Range(0, 180)]
	public float RightRotationLimit;

	[Tooltip("Left rotation limit")]
	[Range(0, 180)]
	public float LeftRotationLimit;

	[Header("Vertical Rotation Settings")]
	[Tooltip("If you want to limit vertical turret rotation")]
	public bool VerticalRotationLimit;

	[Tooltip("Upwards rotation limit")]
	[Range(0, 70)]
	public float UpwardsRotationLimit;

	[Tooltip("Downwards rotation limit")]
	[Range(0, 70)]
	public float DownwardsRotationLimit;

	[Tooltip("Turning speed")]
	[Range(0, 300)]
	public float TurnSpeed;

	[Header("Gun Settings")]
	[Tooltip("Click if you want to use pooling")]
	public bool UsePooling = true;

	[Tooltip("Gun firing rate")]
	public float FireRate = 0.5f;

	[Tooltip("Projectile traveling speed")]
	public float ProjectileSpeed = 100f;

	[Tooltip("How many projectile in this turret")]
	public float ProjectileCount = 100f;

	[Tooltip("Projectile prefabs")]
	public GameObject ProjectilePrefab;

	[Tooltip("Adjust the efficiency of this turret")]
	[Range(3f, 4f)]
	public float Efficiency = 4f;

	[Tooltip("Barrel for instantiating projectile")]
	public Transform[] Barrel;

	[Header("Integração do Projeto")]
	[Tooltip("Se ativo, o disparo tenta registrar a ameaça no rastreador global do projeto.")]
	public bool RegistrarLancamentoNoTracker = true;

	[Tooltip("Se o prefab não tiver Projetil, tenta inicializar qualquer script compatível do projeto.")]
	public bool PermitirFallbackParaProjetilPadrao = true;

	[HideInInspector]
	public Transform target;

	[HideInInspector]
	public Vector3 predictedTargetPosition;

	[Header("Effects (Optional)")]
	[Tooltip("Shoot effect when firing the gun (optional)")]
	public GameObject ShootFX;
	public GameObject BulletShellFX;

	private Vector3 targetlastPosition;
	protected ParticleSystem bulletShellFX_PS;
	protected ParticleSystem shootFX_PS;
	protected float nextFireAllowed;
	protected bool IsAiming = false;
	private IdentidadeUnidade minhaIdentidade;

	protected virtual void Start()
	{
		target = null;
		minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
		if (HorizontalPivot == null || VerticalPivot == null)
		{
			Debug.LogWarning("[AntiMissileGunController] Pivots nao configurados.");
			return;
		}

		if (Barrel == null || Barrel.Length == 0)
		{
			Debug.LogWarning("[AntiMissileGunController] Nenhum barrel configurado.");
			return;
		}

		if (ProjectilePrefab == null)
		{
			Debug.LogWarning("[AntiMissileGunController] ProjectilePrefab ausente.");
			return;
		}

		if (UsePooling)
		{
			PoolDeObjetosCombate.Prewarm(ProjectilePrefab, 100);
		}

		if (BulletShellFX != null)
		{
			BulletShellFX.SetActive(true);
			bulletShellFX_PS = BulletShellFX.GetComponent<ParticleSystem>();
			if (bulletShellFX_PS != null) bulletShellFX_PS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}

		if (ShootFX != null)
		{
			ShootFX.SetActive(true);
			shootFX_PS = ShootFX.GetComponent<ParticleSystem>();
			if (shootFX_PS != null) shootFX_PS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}
	}

	private void FixedUpdate()
	{
		LeadTarget();
		HorizontalRotation();
		VerticalRotation();
		Fire();
	}

	private void LeadTarget()
	{
		if (target == null)
		{
			IsAiming = false;
			return;
		}

		Vector3 targetSpeed = (target.position - targetlastPosition);
		targetSpeed /= Mathf.Max(Time.deltaTime, 0.0001f);

		float distance = Vector3.Distance(transform.position, target.position);
		float projectileTravelTime = distance / Mathf.Max(ProjectileSpeed, 2f);
		Vector3 aimPoint = target.position + targetSpeed * Efficiency / 4f * projectileTravelTime;

		float distance2 = Vector3.Distance(transform.position, aimPoint);
		float projectileTravelTime2 = distance2 / Mathf.Max(ProjectileSpeed, 2f);
		predictedTargetPosition = target.position + targetSpeed * Efficiency / 4f * projectileTravelTime2;

		targetlastPosition = target.position;
	}

	private void HorizontalRotation()
	{
		if (HorizontalPivot == null || target == null) return;

		Vector3 targetPositionInLocalSpace = transform.InverseTransformPoint(predictedTargetPosition);
		targetPositionInLocalSpace.y = 0f;

		Vector3 clamp = targetPositionInLocalSpace;
		if (HorizontalRotationLimit)
		{
			if (targetPositionInLocalSpace.x >= 0f)
				clamp = Vector3.RotateTowards(Vector3.forward, targetPositionInLocalSpace, Mathf.Deg2Rad * RightRotationLimit, 0f);
			else
				clamp = Vector3.RotateTowards(Vector3.forward, targetPositionInLocalSpace, Mathf.Deg2Rad * LeftRotationLimit, 0f);
		}

		Quaternion whereToRotate = Quaternion.LookRotation(clamp);
		HorizontalPivot.localRotation = Quaternion.RotateTowards(HorizontalPivot.localRotation, whereToRotate, TurnSpeed * Time.deltaTime);
	}

	private void VerticalRotation()
	{
		if (VerticalPivot == null || target == null) return;

		Vector3 targetPositionInLocalSpace = HorizontalPivot != null
			? HorizontalPivot.transform.InverseTransformPoint(predictedTargetPosition)
			: transform.InverseTransformPoint(predictedTargetPosition);

		targetPositionInLocalSpace.x = 0f;

		Vector3 clamp = targetPositionInLocalSpace;
		if (VerticalRotationLimit)
		{
			if (targetPositionInLocalSpace.y >= 0f)
				clamp = Vector3.RotateTowards(Vector3.forward, targetPositionInLocalSpace, Mathf.Deg2Rad * UpwardsRotationLimit, 0f);
			else
				clamp = Vector3.RotateTowards(Vector3.forward, targetPositionInLocalSpace, Mathf.Deg2Rad * DownwardsRotationLimit, 0f);
		}

		Quaternion whereToRotate = Quaternion.LookRotation(clamp);
		VerticalPivot.localRotation = Quaternion.RotateTowards(VerticalPivot.localRotation, whereToRotate, 2f * TurnSpeed * Time.deltaTime);

		Vector3 dirTotarget = (predictedTargetPosition - VerticalPivot.position).normalized;
		float angle = Mathf.Abs(Vector3.Angle(VerticalPivot.forward, dirTotarget));
		IsAiming = angle < 5f;
	}

	public void SetTargetGun(Transform targetPosition)
	{
		target = targetPosition;
		if (target != null)
		{
			targetlastPosition = target.position;
		}
	}

	protected virtual void Fire()
	{
		if (target == null || ProjectileCount <= 0 || Time.time <= nextFireAllowed || !IsAiming)
		{
			return;
		}

		if (Barrel == null || Barrel.Length == 0)
		{
			Debug.LogWarning("[AntiMissileGunController] Nenhum barrel configurado.");
			return;
		}

		for (int i = 0; i < Barrel.Length; i++)
		{
			Transform barrel = Barrel[i];
			if (barrel == null)
			{
				continue;
			}

			GameObject projectileGO = UsePooling
				? PoolDeObjetosCombate.Spawn(ProjectilePrefab, barrel.position, barrel.rotation)
				: Instantiate(ProjectilePrefab, barrel.position, barrel.rotation);

			if (projectileGO == null)
			{
				continue;
			}

			InicializarProjetil(projectileGO, barrel);
			ProjectileCount--;
		}

		nextFireAllowed = Time.time + FireRate;

		if (BulletShellFX != null && bulletShellFX_PS != null)
		{
			bulletShellFX_PS.Play();
			Invoke(nameof(StopBulletShellEffect), 1.2f);
		}

		if (ShootFX != null && shootFX_PS != null)
		{
			shootFX_PS.Play();
		}
	}

	private void InicializarProjetil(GameObject projectileGO, Transform barrel)
	{
		if (projectileGO == null)
		{
			return;
		}

		Transform alvoResolvido = ResolverAlvo(target);
		Vector3 posicaoPredita = predictedTargetPosition;
		Vector3 direcaoInicial = posicaoPredita - barrel.position;
		if (direcaoInicial.sqrMagnitude <= 0.001f)
		{
			direcaoInicial = barrel.forward;
		}
		direcaoInicial.Normalize();

		Projetil projetil = projectileGO.GetComponent<Projetil>();
		if (projetil == null && PermitirFallbackParaProjetilPadrao)
		{
			projetil = projectileGO.AddComponent<Projetil>();
		}

		if (projetil != null)
		{
			projetil.SetDono(transform.root != null ? transform.root.gameObject : gameObject);
			projetil.velocidade = ProjectileSpeed;
			projetil.SetDirecao(direcaoInicial);
			if (alvoResolvido != null)
			{
				projetil.SetAlvo(alvoResolvido);
				projetil.curvaDePerseguicao = Mathf.Max(projetil.curvaDePerseguicao, 150f);
			}
		}
		else
		{
			AntiMissileProjectile antiMissileProjectile = projectileGO.GetComponent<AntiMissileProjectile>();
			if (antiMissileProjectile != null)
			{
				antiMissileProjectile.transform.rotation = Quaternion.LookRotation(direcaoInicial);
				antiMissileProjectile.Speed = ProjectileSpeed;
			}
		}

		if (RegistrarLancamentoNoTracker && alvoResolvido != null)
		{
			MissileThreatTracker.RegistrarLancamento(
				projectileGO,
				this,
				posicaoPredita,
				alvoResolvido,
				ProjectileSpeed,
				true);
		}
	}

	private Transform ResolverAlvo(Transform alvoOriginal)
	{
		if (alvoOriginal == null)
		{
			return null;
		}

		Projetil projetil = alvoOriginal.GetComponentInParent<Projetil>();
		if (projetil != null)
		{
			return projetil.transform;
		}

		Rigidbody rb = alvoOriginal.GetComponentInParent<Rigidbody>();
		if (rb != null)
		{
			return rb.transform;
		}

		IdentidadeUnidade identidade = alvoOriginal.GetComponentInParent<IdentidadeUnidade>();
		if (identidade != null)
		{
			return identidade.transform;
		}

		return alvoOriginal.root != null ? alvoOriginal.root : alvoOriginal;
	}

	void StopBulletShellEffect()
	{
		if (bulletShellFX_PS != null)
		{
			bulletShellFX_PS.Stop();
		}
	}
}
