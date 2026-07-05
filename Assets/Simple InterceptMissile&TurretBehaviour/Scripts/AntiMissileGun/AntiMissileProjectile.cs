using System.Collections;
using UnityEngine;

public class AntiMissileProjectile : MonoBehaviour
{
	[Header("Projectile settings")]
	[Tooltip("Projectile traveling speed")]
	[HideInInspector]
	public float Speed;

	[Tooltip("Projectile life time")]
	public float TimeTodestroy = 8f;

	[Tooltip("Projectile Explosion FX (Optional)")]
	public GameObject Explosion;

	private bool estaVivo;
	private float expiraEm;
	private GameObject dono;

	private void OnEnable()
	{
		estaVivo = true;
		expiraEm = Time.time + Mathf.Max(0.05f, TimeTodestroy);
	}

	private void Update()
	{
		if (!estaVivo)
		{
			return;
		}

		if (Time.time >= expiraEm)
		{
			Liberar();
			return;
		}

		transform.Translate(Vector3.forward * Speed * Time.deltaTime, Space.Self);
	}

	protected virtual void OnCollisionEnter(Collision col)
	{
		ProcessarImpacto(col != null ? col.gameObject : null);
	}

	private void OnTriggerEnter(Collider other)
	{
		ProcessarImpacto(other != null ? other.gameObject : null);
	}

	public void SetDono(GameObject novoDono)
	{
		dono = novoDono;
	}

	IEnumerator DestroyDelay()
	{
		yield return new WaitForSeconds(TimeTodestroy);
		Liberar();
	}

	public virtual void OnobjectReuse(Vector3 target, float speed)
	{
		transform.LookAt(target);
		Speed = speed;
		estaVivo = true;
		expiraEm = Time.time + Mathf.Max(0.05f, TimeTodestroy);
	}

	private void ProcessarImpacto(GameObject alvo)
	{
		if (!estaVivo || alvo == null)
		{
			return;
		}

		if (dono != null && (alvo == dono || alvo.transform.IsChildOf(dono.transform)))
		{
			return;
		}

		estaVivo = false;

		if (Explosion != null)
		{
			PoolDeObjetosCombate.SpawnTemporario(Explosion, transform.position, transform.rotation, 2f);
		}

		Liberar();
	}

	private void Liberar()
	{
		estaVivo = false;
		PoolDeObjetosCombate.Release(gameObject);
	}
}
