import assert from 'node:assert/strict';
import test from 'node:test';
import { meshFixture } from '../fixtures/meshFixture.mjs';
import { costText, exactMeshNode, filterMeshNodes, meshLayout, nodeStatus, usableSelectedRoute } from '../../src/features/network/meshModel.js';

test('Network search handles normalized MAC/ID, names and reachable/isolated filters', () => {
  const snapshot = meshFixture();
  assert.equal(exactMeshNode(snapshot, ' a4:cf:12:8b:40:01 ').id, 'ph');
  assert.equal(exactMeshNode(snapshot, 'POWER').id, 'power');
  assert.equal(exactMeshNode(snapshot, 'unknown'), null);
  assert.equal(filterMeshNodes(snapshot, { search: 'Nutrient' })[0].id, 'ph');
  assert.deepEqual(filterMeshNodes(snapshot, { nodeFilter: 'unreachable' }).map(n => n.id), ['isolated']);
});
test('Only real available selected-tree edges can be highlighted as a route', () => {
  const snapshot = meshFixture({ selectedId: 'power' });
  assert.equal(usableSelectedRoute(snapshot, 'power').hopCount, 2);
  assert.equal(usableSelectedRoute(snapshot, 'ph'), null);
  snapshot.selectedRoute.edgeIds[0] = 'gb';
  assert.equal(usableSelectedRoute(snapshot, 'power'), null);
});
test('No-route and zero-hop gateway results keep null and zero meanings distinct', () => {
  const isolated = meshFixture({ selectedId: 'isolated' });
  assert.equal(usableSelectedRoute(isolated, 'isolated').isReachable, false);
  const gateway = meshFixture({ selectedId: 'gw' });
  assert.equal(usableSelectedRoute(gateway, 'gw').hopCount, 0);
  assert.equal(costText(0), '0 mJ');
  assert.equal(costText(null), 'Unavailable');
  gateway.selectedRoute.totalPowerCostMilliJoules = null;
  assert.equal(usableSelectedRoute(gateway, 'gw'), null);
});
test('Schematic positions remain stable during health changes and isolate unlinked nodes', () => {
  const normal = meshLayout(meshFixture()), failed = meshLayout(meshFixture({ failedNodes: ['ph'] }));
  assert.deepEqual([...normal.points], [...failed.points]);
  assert.ok(normal.points.get('isolated').y > 550);
  assert.equal(nodeStatus(meshFixture({ failedNodes: ['ph'] }).nodes.find(n => n.id === 'ph')), 'Simulated failure');
});
