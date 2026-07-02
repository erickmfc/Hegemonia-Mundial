using UnityEngine;

public class MediumProjectile : AntiMissileProjectile {
	[Header("Child class")]
	[Tooltip("parameters of child class")]
	
	public GameObject BodyExplosion_FX;

	protected override void OnCollisionEnter(Collision col)
	{
		Destroy(gameObject, 0.1f); // o pool do projeto já é tratado pelo AntiMissileProjectile

		var layer = LayerMask.LayerToName(col.gameObject.layer);

		if(layer == "EnemyPlane" || layer == "EnemyTank")
		{
			if(Explosion != null)
				Instantiate(Explosion, transform.position, transform.rotation);
		}
		else if(layer == "EnemySoldier")
		{
			if(BodyExplosion_FX != null)
				Instantiate(BodyExplosion_FX, transform.position, transform.rotation);
		}
		else
			if(Explosion != null)
				Instantiate(Explosion, transform.position, transform.rotation);	
	}
}
