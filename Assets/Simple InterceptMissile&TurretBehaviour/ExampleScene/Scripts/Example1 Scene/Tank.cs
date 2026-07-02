using UnityEngine;
using UnityEngine.AI;
public class Tank : MonoBehaviour {
	public float Speed;
	public float Health;
	public float TankLifeTime;
	public float[] RandomTurnRate;
	public float LerpSpeed;
	public GameObject DamageFX;
	public GameObject ExplosionFX;

	private Rigidbody _rb;
	private bool _isDead;
	private NavMeshAgent _agent;
	private Transform[] _waypoints;
	private Vector3 _targetPoint;

	// Use this for initialization
	void Start () {

		_rb = GetComponent<Rigidbody>();

		if (RandomTurnRate != null && RandomTurnRate.Length >= 2)
			transform.Rotate(0f,Random.Range(RandomTurnRate[0],RandomTurnRate[1]),0f);

		if (DamageFX) DamageFX.SetActive(false);
		
		if (ExplosionFX) ExplosionFX.SetActive(false);
		
		_agent = GetComponent<NavMeshAgent>();
		
		Manager manager = Manager.GetInstance();
		_waypoints = manager != null ? manager.Waypoints1 : null;
		DefinirDestinoAleatorio();

		NavAgentControl(true, false);
		
		Invoke("Destroy", TankLifeTime);

	}
	
	// Update is called once per frame
	void Update () {
		if (!_agent || !_agent.enabled || !_agent.isOnNavMesh) return;

		if(_isDead){
			Speed = Mathf.Lerp(Speed, 0, LerpSpeed * Time.fixedDeltaTime);
		}	

		// Generate a new Quaternion representing the rotation we should have
		Vector3 velocidadeDesejada = _agent.desiredVelocity;
		if (velocidadeDesejada.sqrMagnitude > 0.0001f)
		{
			Quaternion newRot = Quaternion.LookRotation(velocidadeDesejada);
			transform.rotation = Quaternion.Slerp(transform.rotation, newRot, Time.deltaTime * 5.0f);
		}

		if(Vector3.Distance(transform.position, _targetPoint) < _agent.stoppingDistance)
		{
			DefinirDestinoAleatorio();
		}

		if (_agent.isPathStale || 
			!_agent.hasPath   ||
			_agent.pathStatus!=NavMeshPathStatus.PathComplete) 
		{
			DefinirDestinoAleatorio();
		}

	}

	private bool DefinirDestinoAleatorio()
	{
		if (!_agent || !_agent.enabled || !_agent.isOnNavMesh || _waypoints == null || _waypoints.Length == 0)
			return false;

		Transform waypoint = _waypoints[Random.Range(0, _waypoints.Length)];
		if (!waypoint) return false;

		RandomPoint(waypoint.position, 2f, out _targetPoint);
		return true;
	}

	void OnCollisionEnter(Collision collision)
	{	
		if(LayerMask.LayerToName(collision.gameObject.layer) != "Projectile") return;
	
		if(_isDead) return;
		
		Health --;
		if(Health <= 0)
		{	
			_isDead = true;
			DamageFX.SetActive(true);
			_rb.useGravity = true;
			Invoke("Destroy", 10.0f);		
		}
	}

	public void NavAgentControl( bool positionUpdate, bool rotationUpdate )
	{
		if (_agent)
		{
			_agent.updatePosition = positionUpdate;
			_agent.updateRotation = rotationUpdate;
		}
	}

	//calculate random point for movement on navigation mesh
    private void RandomPoint(Vector3 center, float range, out Vector3 result)
	{
		//clear previous target point
		result = Vector3.zero;
		
		//try to find a valid point on the navmesh with an upper limit (10 times)
		for (int i = 0; i < 10; i++)
		{
			//find a point in the movement radius
			Vector3 randomPoint = center + (Vector3)Random.insideUnitCircle * range;
			randomPoint.y = 0;
			NavMeshHit hit;

			//if the point found is a valid target point, set it and continue
			if (NavMesh.SamplePosition(randomPoint, out hit, 2f, NavMesh.AllAreas)) 
			{
				result = hit.position;
				break;
			}
		}
		
		//set the target point as the new destination
		if (_agent && _agent.enabled && _agent.isOnNavMesh)
			_agent.SetDestination(result);
	}

	void Destroy()
	{			
		if(!DamageFX.activeInHierarchy)
			DamageFX.SetActive(true);
		DamageFX.transform.SetParent(null);
		ExplosionFX.SetActive(true);
		ExplosionFX.transform.SetParent(null);
		Destroy(gameObject);
	}
}
