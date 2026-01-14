import { useEffect, useState } from 'react';
import type { HealthResponse } from '../types/health';
import api from '../services/api';

const HealthCheck = () => {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchHealth = async () => {
      try {
        setLoading(true);
        setError(null);
        const data = await api.getHealth();
        setHealth(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to fetch health status');
        console.error('Health check failed:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchHealth();
    
    // Refresh health status every 30 seconds
    const interval = setInterval(fetchHealth, 30000);
    
    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <div className="health-check">
        <h3>Backend Health Status</h3>
        <p>Loading...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="health-check error">
        <h3>Backend Health Status</h3>
        <p className="status-error">❌ Connection Error</p>
        <p className="error-message">{error}</p>
      </div>
    );
  }

  return (
    <div className="health-check">
      <h3>Backend Health Status</h3>
      <div className="status-container">
        <p className={`status ${health?.status === 'Healthy' ? 'status-healthy' : 'status-unhealthy'}`}>
          {health?.status === 'Healthy' ? '✅' : '⚠️'} {health?.status}
        </p>
        {health?.checks && (
          <div className="checks">
            {Object.entries(health.checks).map(([key, value]) => (
              <div key={key} className="check-item">
                <strong>{key}:</strong> {value.status} 
                {value.message && <span className="check-message"> - {value.message}</span>}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

export default HealthCheck;
