import { Logger } from '@nestjs/common';
import { readConfig } from './infrastructure/configuration/environment.js';
import { createGateway } from './composition/bootstrap.js';

const config = readConfig();
const app = await createGateway(config);
app.enableShutdownHooks();
try {
  await app.listen(config.port, config.host);
  Logger.log(`Gateway: http://${config.host}:${config.port}`, 'Bootstrap');
  Logger.log(`Inventory: ${config.upstream.origin}`, 'Bootstrap');
  Logger.log(`Identity: ${config.identityUpstream.origin}`, 'Bootstrap');
  Logger.log(`Orders: ${config.ordersUpstream.origin}`, 'Bootstrap');
  Logger.log(`Reporting: ${config.reportingUpstream.origin}`, 'Bootstrap');
} catch (error) {
  Logger.error(error, 'Bootstrap');
  process.exitCode = 1;
  await app.close();
}
